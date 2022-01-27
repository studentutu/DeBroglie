﻿using DeBroglie.Models;
using DeBroglie.Rot;
using DeBroglie.Topo;
using DeBroglie.Trackers;
using DeBroglie.Wfc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeBroglie.Constraints
{
    [Obsolete("Use ConnectedConstraint instead")]
    public class EdgedPathConstraint : ITileConstraint
    {
        private TilePropagatorTileSet pathTileSet;

        private SelectedTracker pathSelectedTracker;

        private TilePropagatorTileSet endPointTileSet;

        private SelectedTracker endPointSelectedTracker;

        private PathConstraintUtils.SimpleGraph graph;

        private IDictionary<Direction, TilePropagatorTileSet> tilesByExit;
        private IDictionary<Direction, SelectedTracker> trackerByExit;
        private IDictionary<Tile, ISet<Direction>> actualExits { get; set; }


        /// <summary>
        /// For each tile on the path, the set of direction values that paths exit out of this tile.
        /// </summary>
        public IDictionary<Tile, ISet<Direction>> Exits { get; set; }

        /// <summary>
        /// Set of points that must be connected by paths.
        /// If EndPoints and EndPointTiles are null, then EdgedPathConstraint ensures that all path cells
        /// are connected.
        /// </summary>
        public Point[] EndPoints { get; set; }

        /// <summary>
        /// Set of tiles that must be connected by paths.
        /// If EndPoints and EndPointTiles are null, then EdgedPathConstraint ensures that all path cells
        /// are connected.
        /// </summary>
        public ISet<Tile> EndPointTiles { get; set; }

        /// <summary>
        /// If set, Exits is augmented with extra copies as dictated by the tile rotations
        /// </summary>
        public TileRotation TileRotation { get; set; }

        /// <summary>
        /// If set, configures the propagator to choose tiles that lie on the path first.
        /// This can help avoid contradictions in many cases
        /// </summary>
        public bool UsePickHeuristic { get; set; }

        public EdgedPathConstraint(IDictionary<Tile, ISet<Direction>> exits, Point[] endPoints = null, TileRotation tileRotation = null)
        {
            this.Exits = exits;
            this.EndPoints = endPoints;
            this.TileRotation = tileRotation;
        }


        public void Init(TilePropagator propagator)
        {
            
            ISet<Tile> actualEndPointTiles;
            if (TileRotation != null)
            {
                actualExits = new Dictionary<Tile, ISet<Direction>>();
                foreach (var kv in Exits)
                {
                    foreach (var rot in TileRotation.RotationGroup)
                    {
                        if (TileRotation.Rotate(kv.Key, rot, out var rtile))
                        {
                            Direction Rotate(Direction d)
                            {
                                return TopoArrayUtils.RotateDirection(propagator.Topology.AsGridTopology().Directions, d, rot);
                            }
                            var rexits = new HashSet<Direction>(kv.Value.Select(Rotate));
                            actualExits[rtile] = rexits;
                        }
                    }
                }
                actualEndPointTiles = EndPointTiles == null ? null : new HashSet<Tile>(TileRotation.RotateAll(EndPointTiles));
            }
            else
            {
                actualExits = Exits;
                actualEndPointTiles = EndPointTiles;
            }

            pathTileSet = propagator.CreateTileSet(Exits.Keys);
            pathSelectedTracker = propagator.CreateSelectedTracker(pathTileSet);
            endPointTileSet = EndPointTiles != null ? propagator.CreateTileSet(actualEndPointTiles) : null;
            endPointSelectedTracker = EndPointTiles != null ? propagator.CreateSelectedTracker(endPointTileSet) : null;
            graph = CreateEdgedGraph(propagator.Topology);


            tilesByExit = actualExits
                .SelectMany(kv => kv.Value.Select(e => Tuple.Create(kv.Key, e)))
                .GroupBy(x => x.Item2, x => x.Item1)
                .ToDictionary(g => g.Key, propagator.CreateTileSet);

            trackerByExit = tilesByExit
                .ToDictionary(kv => kv.Key, kv => propagator.CreateSelectedTracker(kv.Value));

            Check(propagator, true);
        }

        public void Check(TilePropagator propagator)
        {
            Check(propagator, false);
        }

        private void Check(TilePropagator propagator, bool init)
        {

            var topology = propagator.Topology;
            var indices = topology.Width * topology.Height * topology.Depth;

            var nodesPerIndex = topology.DirectionsCount + 1;

            // Initialize couldBePath and mustBePath based on wave possibilities
            var couldBePath = new bool[indices * nodesPerIndex];
            var mustBePath = new bool[indices * nodesPerIndex];
            var exitMustBePath = new bool[indices * nodesPerIndex];
            foreach (var kv in trackerByExit)
            {
                var exit = kv.Key;
                var tracker = kv.Value;
                for (int i = 0; i < indices; i++)
                {
                    var ts = tracker.GetQuadstate(i);
                    couldBePath[i * nodesPerIndex + 1 + (int)exit] = ts.Possible();
                    // Cannot put this in mustBePath these points can be disconnected, depending on topology mask
                    exitMustBePath[i * nodesPerIndex + 1 + (int)exit] = ts.IsYes();
                }
            }
            for (int i = 0; i < indices; i++)
            {
                var pathTs = pathSelectedTracker.GetQuadstate(i);
                couldBePath[i * nodesPerIndex] = pathTs.Possible();
                mustBePath[i * nodesPerIndex] = pathTs.IsYes();
            }
            // Select relevant cells, i.e. those that must be connected.
            var hasEndPoints = EndPoints != null || EndPointTiles != null;
            bool[] relevant;
            if (!hasEndPoints)
            {
                // Basically equivalent to EndPoints = pathTileSet
                relevant = mustBePath;
            }
            else
            {
                relevant = new bool[indices * nodesPerIndex];

                var relevantCount = 0;
                if (EndPoints != null)
                {
                    foreach (var endPoint in EndPoints)
                    {
                        var index = topology.GetIndex(endPoint.X, endPoint.Y, endPoint.Z);
                        relevant[index * nodesPerIndex] = true;
                        relevantCount++;
                    }
                }
                if (EndPointTiles != null)
                {
                    for (int i = 0; i < indices; i++)
                    {
                        if (endPointSelectedTracker.IsSelected(i))
                        {
                            relevant[i * nodesPerIndex] = true;
                            relevantCount++;
                        }
                    }
                }
                if (relevantCount == 0)
                {
                    // Nothing to do.
                    return;
                }
            }

            if (init)
            {
                for (int i = 0; i < indices; i++)
                {
                    if (relevant[i * nodesPerIndex])
                    {
                        topology.GetCoord(i, out var x, out var y, out var z);
                        propagator.Select(x, y, z, pathTileSet);
                    }
                }
            }

            var walkable = couldBePath;

            var info = PathConstraintUtils.GetArticulationPoints(graph, walkable, relevant);
            var isArticulation = info.IsArticulation;

            if (info.ComponentCount > 1)
            {
                propagator.SetContradiction("Edged path constraint found multiple connected components.", this);
                return;
            }


            // All articulation points must be paths,
            // So ban any other possibilities
            for (var i = 0; i < indices; i++)
            {
                topology.GetCoord(i, out var x, out var y, out var z);
                if (isArticulation[i * nodesPerIndex] && !mustBePath[i * nodesPerIndex])
                {
                    propagator.Select(x, y, z, pathTileSet);
                }
                for (var d = 0; d < topology.DirectionsCount; d++)
                {
                    if(isArticulation[i * nodesPerIndex + 1 + d] && !exitMustBePath[i * nodesPerIndex + 1 + d])
                    {
                        if (tilesByExit.TryGetValue((Direction)d, out var exitTiles))
                        {
                            propagator.Select(x, y, z, exitTiles);
                        }
                    }
                }
            }

            // Any path tiles / EndPointTiles not in the connected component aren't safe to add.
            if (info.ComponentCount > 0)
            {
                var component = info.Component;
                var actualEndPointTileSet = hasEndPoints ? endPointTileSet : pathTileSet;
                if (actualEndPointTileSet != null)
                {
                    for (int i = 0; i < indices; i++)
                    {
                        if (component[i * nodesPerIndex] == null)
                        {
                            topology.GetCoord(i, out var x, out var y, out var z);
                            propagator.Ban(x, y, z, actualEndPointTileSet);
                        }
                    }
                }
            }
        }


        private static readonly int[] Empty = { };

        /// <summary>
        /// Creates a grpah where each index in the original topology
        /// has 1+n nodes in the graph - one for the initial index
        /// and one for each direction leading out of it.
        /// </summary>
        private static PathConstraintUtils.SimpleGraph CreateEdgedGraph(ITopology topology)
        {
            var nodesPerIndex = topology.DirectionsCount + 1;

            var nodeCount = topology.IndexCount * nodesPerIndex;

            var neighbours = new int[nodeCount][];

            int GetNodeId(int index) => index * nodesPerIndex;

            int GetDirNodeId(int index, Direction direction) => index * nodesPerIndex + 1 + (int)direction;

            foreach (var i in topology.GetIndices())
            {
                var n = new List<int>();
                for(var d=0;d<topology.DirectionsCount;d++)
                {
                    var direction = (Direction)d;
                    if (topology.TryMove(i, direction, out var dest, out var inverseDir, out var _))
                    {
                        // The central node connects to the direction node
                        n.Add(GetDirNodeId(i, direction));
                        // The diction node connects to the central node
                        // and the opposing direction node
                        neighbours[GetDirNodeId(i, direction)] =
                            new[] { GetNodeId(i), GetDirNodeId(dest, inverseDir) };
                    }
                    else
                    {
                        neighbours[GetDirNodeId(i, direction)] = Empty;
                    }
                }
                neighbours[GetNodeId(i)] = n.ToArray();
            }

            return new PathConstraintUtils.SimpleGraph
            {
                NodeCount = nodeCount,
                Neighbours = neighbours,
            };
        }

        internal IIndexPicker GetHeuristic(
                IFilteredIndexPicker filteredIndexPicker,
                TilePropagator propagator)
        {
            return new FollowPathHeuristic(
                filteredIndexPicker, propagator, this);
        }

        private class FollowPathHeuristic : IIndexPicker
        {
            private readonly IFilteredIndexPicker filteredIndexPicker;

            private readonly TilePropagator propagator;

            private readonly EdgedPathConstraint pathConstraint;

            public FollowPathHeuristic(
                IFilteredIndexPicker filteredIndexPicker,
                TilePropagator propagator,
                EdgedPathConstraint pathConstraint)
            {
                this.filteredIndexPicker = filteredIndexPicker;
                this.propagator = propagator;
                this.pathConstraint = pathConstraint;
            }

            public void Init(WavePropagator wavePropagator)
            {
                filteredIndexPicker.Init(wavePropagator);
            }

            public int GetRandomIndex(Func<double> randomDouble)
            {
                var topology = propagator.Topology;
                var t = pathConstraint.pathSelectedTracker;
                // Find cells that could potentially be paths, and are next to 
                // already selected path. In tileSpace
                var highPriority = new List<int>();
                var mediumPrioiry = new List<int>();
                var lowPriority = new List<int>();
                foreach (var i in topology.GetIndices())
                {
                    var qs = t.GetQuadstate(i);
                    if (qs.IsYes())
                    {
                        highPriority.Add(i);
                        continue;
                    }
                    if (qs.IsNo())
                    {
                        lowPriority.Add(i);
                        continue;
                    }
                    // Determine if any neighbours exit onto this tile
                    var found = false;
                    for (var d = 0; d < topology.DirectionsCount; d++)
                    {
                        if (topology.TryMove(i, (Direction)d, out var i2, out var inverseDirection, out var _))
                        {
                            if (pathConstraint.trackerByExit.TryGetValue(inverseDirection, out var tracker))
                            {
                                var s2 = tracker.GetQuadstate(i2);
                                if (s2.IsYes())
                                {
                                    mediumPrioiry.Add(i);
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!found)
                    {
                        lowPriority.Add(i);
                    }
                }

                var index = filteredIndexPicker.GetRandomIndex(randomDouble, highPriority);
                if (index != -1)
                    return index;
                index = filteredIndexPicker.GetRandomIndex(randomDouble, mediumPrioiry);
                if (index != -1)
                    return index;
                index = filteredIndexPicker.GetRandomIndex(randomDouble, lowPriority);
                return index;
            }
        }
    }
}
