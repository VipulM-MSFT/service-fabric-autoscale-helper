﻿namespace NodeManager
{
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Fabric.Description;
    using System.Fabric.Health;
    using System.Fabric.Query;
    using System.Threading;
    using System.Threading.Tasks;

    internal class NodeManagerService : ActorService
    {
        private static readonly TimeSpan ScanInterval;
        private static readonly TimeSpan ClientOperationTimeout;
        private static readonly TimeSpan DownNodeGraceInterval;
        private static readonly bool SkipNodesUnderFabricUpgrade;

        public NodeManagerService(
            StatefulServiceContext context,
            ActorTypeInformation actorTypeInfo,
            Func<ActorService, ActorId, ActorBase> actorFactory = null,
            Func<ActorBase, IActorStateProvider, IActorStateManager> stateManagerFactory = null,
            IActorStateProvider stateProvider = null, ActorServiceSettings settings = null)
            : base(context, actorTypeInfo, actorFactory, stateManagerFactory, stateProvider, settings)
        {
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await base.RunAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RemoveScaledInNodesAsync(cancellationToken);
                }
                catch (Exception e)
                {
                    ActorEventSource.Current.ServiceError(
                        this.Context,
                        "Failed to remove scaled-in nodes, Error = {0}",
                        e);
                }


                await Task.Delay(ScanInterval, cancellationToken);
            }
        }

        /// <summary>
        /// Remove the nodes that are scaled in but still managed by Service Fabric. These nodes show up as down nodes.
        /// </summary>
        private async Task RemoveScaledInNodesAsync(CancellationToken cancellationToken)
        {
            var client = new FabricClient();

            if (SkipNodesUnderFabricUpgrade)
            {
                var upgradeInProgress = await IsFabricUpgradeInProgressAsync(client, cancellationToken);


                if (upgradeInProgress)
                {
                    ActorEventSource.Current.ServiceMessage(
                        this.Context,
                        "Skipping removing scaled-in nodes as fabric upgrade is in progress.");

                    Context.CodePackageActivationContext.ReportApplicationHealth(
                        new HealthInformation("NodeManager", "FabricUpgrade", HealthState.Ok)
                        {
                            RemoveWhenExpired = true,
                            TimeToLive = TimeSpan.FromSeconds(300),
                            Description = "Skipping removing scaled-in nodes as fabric upgrade is in progress."
                        });
                    return;
                }
            }

            var nodesToRemove = await GetNodesToRemoveAsync(client, cancellationToken);

            Context.CodePackageActivationContext.ReportApplicationHealth(
                new HealthInformation("NodeManager", "Scan", HealthState.Ok)
                {
                    RemoveWhenExpired = true,
                    TimeToLive = TimeSpan.FromSeconds(300),
                    Description = $"Completed scan to remove scaled-in nodes. Found {nodesToRemove.Count} nodes."
                });

            await RemoveNodesAsync(client, nodesToRemove, cancellationToken);
        }

        private async Task RemoveNodesAsync(
            FabricClient client,
            IList<Node> nodesToRemove,
            CancellationToken cancellationToken)
        {
            foreach (var node in nodesToRemove)
            {
                try
                {
                    ActorEventSource.Current.ServiceMessage(
                        this.Context,
                        "Removing the state of node {0}.",
                        node.NodeName);

                    await client.ClusterManager.RemoveNodeStateAsync(
                        node.NodeName,
                        ClientOperationTimeout,
                        cancellationToken);

                    Context.CodePackageActivationContext.ReportApplicationHealth(
                        new HealthInformation("NodeManager", node.NodeName, HealthState.Ok)
                        {
                            RemoveWhenExpired = true,
                            TimeToLive = TimeSpan.FromSeconds(300),
                            Description = "Removed scaled-in node state successfully."
                        });
                }
                catch (Exception e)
                {
                    ActorEventSource.Current.ServiceWarning(
                        this.Context,
                        "Error in removing state of the node {0}. Exception {1}",
                        node.NodeName,
                        e.ToString());

                    Context.CodePackageActivationContext.ReportApplicationHealth(
                        new HealthInformation("NodeManager", node.NodeName, HealthState.Warning)
                        {
                            RemoveWhenExpired = true,
                            TimeToLive = TimeSpan.FromSeconds(300),
                            Description = $"Failed to remove scaled-in node state, Error = {e.ToString()}"
                        });
                }
            }
        }

        private async Task<IList<Node>> GetNodesToRemoveAsync(
            FabricClient client,
            CancellationToken cancellationToken)
        {
            var nodesToRemove = new List<Node>();

            var queryDescription = new NodeQueryDescription();
            queryDescription.ContinuationToken = null;

            do
            {
                var nodeList = await client.QueryManager.GetNodePagedListAsync(
                    queryDescription,
                    ClientOperationTimeout,
                    cancellationToken);
                foreach (var node in nodeList)
                {
                    if (IsMyType(node))
                    {
                        // do not remove the nodes where this service is running
                        continue;
                    }

                    if (node.NodeStatus == NodeStatus.Down)
                    {
                        // is down long enough
                        if (IsDownLongEnough(node))
                        {
                            nodesToRemove.Add(node);
                        }
                    }
                }

                queryDescription.ContinuationToken = nodeList.ContinuationToken;
            } while (queryDescription.ContinuationToken != null);

            return nodesToRemove;
        }

        private bool IsDownLongEnough(Node node)
        {
            var downInterval = DateTime.UtcNow.Subtract(node.NodeDownAt);

            ActorEventSource.Current.ServiceMessage(
                this.Context,
                "Node {0} is down for {1} time.",
                node.NodeName,
                downInterval);

            return (downInterval.CompareTo(DownNodeGraceInterval) > 0);
        }


        private bool IsMyType(Node node)
        {
            return (string.Compare(
                this.Context.NodeContext.NodeType,
                node.NodeType,
                StringComparison.OrdinalIgnoreCase) == 0);
        }

        private async Task<bool> IsFabricUpgradeInProgressAsync(
            FabricClient client,
            CancellationToken cancellationToken)
        {
            ActorEventSource.Current.ServiceMessage(
                this.Context,
                "Checking if FabricUpgrade is in progress or not.");

            var upgradeProgress = await client.ClusterManager.GetFabricUpgradeProgressAsync(ClientOperationTimeout, cancellationToken);

            if ((upgradeProgress.UpgradeState == FabricUpgradeState.RollingBackInProgress) ||
                (upgradeProgress.UpgradeState == FabricUpgradeState.RollingForwardInProgress) ||
                (upgradeProgress.UpgradeState == FabricUpgradeState.RollingForwardPending))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static NodeManagerService()
        {
            ScanInterval = TimeSpan.FromSeconds(60);
            ClientOperationTimeout = TimeSpan.FromSeconds(30);
            DownNodeGraceInterval = TimeSpan.FromSeconds(120);
            SkipNodesUnderFabricUpgrade = true;
        }
    }

}
