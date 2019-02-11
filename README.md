---
services: service-fabric
platforms: dotnet, windows
author: raunakpandya, vipulm-msft, prashantbhutani90
---

[![Build status](https://ci.appveyor.com/api/projects/status/9ygqxfgcckkkc6mp/branch/master?svg=true)](https://ci.appveyor.com/project/prashantbhutani90/service-fabric-autoscale-helper/branch/stateless?svg=true)

# service-fabric-autoscale-helper
Service Fabric application that manages autoscaling nodes in VMSS based [Microsoft Azure Service Fabric](https://azure.microsoft.com/services/service-fabric/) cluster.

> Use Service Fabric AutoScale Helper as a supported mechanism for managing autoscaled VMSS nodes in a Service Fabric Cluster.

This application should be used for:
- Managing autoscaled-in nodes for [durability level of bronze](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-cluster-scale-up-down#behaviors-you-may-observe-in-service-fabric-explorer) in a Service Fabric cluster
- Getting out of a stuck situation where nodes are in down state for a long period.

### `Use case scenario for stateless-only workloads`
`We have seen developers who mostly use stateless-only workloads, end up having service fabric cluster in weird state due to autoscaling-in of nodes. If you have seen these issues or planning to use Service Fabric for stateless only workloads, read `[`ClusterPlanningForStatelessServiceWorkload `](https://github.com/prashantbhutani90/service-fabric-autoscale-helper/blob/stateless/ClusterPlanningForStatelessServiceWorkload.md)`in this repo.`

## About this application
The application looks for nodes which are down for a long period in the cluster, and trigger [Remove-ServiceFabricNodeState](https://docs.microsoft.com/en-us/powershell/module/servicefabric/remove-servicefabricnodestate?view=azureservicefabricps) API.

Autoscale helper service provides a common framework to manage and resolve autoscale related issues, which would then be ported back in the Service Fabric code.

## Application Parameters
The service allows users to change node removal logic as per their need.

### Parameter Descripiton
|Parameter|Description|
|:-|:-|
|NodeManagerActorService_ScanIntervalInSeconds|How often the application should scan for the nodes ready for removal. <br/><br/>Default value is `60` seconds.|
|NodeManagerActorService_ClientOperationTimeoutInSeconds|How long does the client wait for the response from cluster. <br/><br/>Default value is `30` seconds.|
|NodeManagerActorService_DownNodeGraceIntervalInSeconds|How long the node should be down for the service to consider it gone for good. <br/><br/>Default value is `120` seconds.|
|NodeManagerActorService_SkipNodesUnderFabricUpgrade|The service should consider nodes under fabric upgrade for removal or not.  <br/><br/>Default value is `true`.|

## Usage

## Build Application
If you don not have the autoscale helper application already deployed in the cluster, build the application and then deploy it in your cluster. Once deployed, you can enable the application with specific application parameters.

[Setup your development environment with Visual Studio 2017](https://docs.microsoft.com/azure/service-fabric/service-fabric-get-started).

Open PowerShell command prompt and run `build.ps1` script. It should produce an output like below.

```PowerShell
PS E:\service-fabric-autoscale-helper> .\build.ps1
Restore completed in 46.43 ms for E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager\NodeManager.csproj
.
Restore completed in 46.43 ms for E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager.Interfaces\NodeMan
ager.Interfaces.csproj.
Restore completed in 1.22 ms for E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager.Interfaces\NodeMana
ger.Interfaces.csproj.
Restore completed in 1.26 ms for E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager\NodeManager.csproj.
NodeManager.Interfaces -> E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager.Interfaces\bin\Release\net
coreapp2.0\win7-x64\NodeManager.Interfaces.dll
NodeManager -> E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager\bin\Release\netcoreapp2.0\win7-x64\No
deManager.dll
NodeManager.Interfaces -> E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager.Interfaces\bin\Release\net
coreapp2.0\win7-x64\NodeManager.Interfaces.dll
NodeManager -> E:\service-fabric-autoscale-helper\src\AutoscaleManager\NodeManager\bin\Release\netcoreapp2.0\win7-x64\No
deManager.dll
NodeManager -> E:\service-fabric-autoscale-helper\src\AutoscaleManager\AutoscaleManager\pkg\Release\NodeManagerPkg\Code\
AutoscaleManager -> E:\service-fabric-autoscale-helper\src\AutoscaleManager\AutoscaleManager\pkg\Release
PS E:\service-fabric-autoscale-helper>
```

By default the script will create a `release` package of the application in `src\AutoscaleManager\AutoscaleManager\pkg\Release` folder. 

## Deploy Application

- Open PowerShell command prompt and go to the root of the repository.

- Connect to the Service Fabric Cluster where you want to deploy the application using [`Connect-ServiceFabricCluster`](https://docs.microsoft.com/en-us/powershell/module/servicefabric/connect-servicefabriccluster?view=azureservicefabricps) PowerShell command. 

- Deploy the application using the following PowerShell command.

  ```PowerShell
  . src\AutoscaleManager\AutoscaleManager\Scripts\Deploy-FabricApplication.ps1 -ApplicationPackagePath 'src\AutoscaleManager\AutoscaleManager\pkg\Release' -PublishProfileFile 'src\AutoscaleManager\AutoscaleManager\PublishProfiles\Cloud.xml' -UseExistingClusterConnection
  ```

- Deploy the application using the following PowerShell command, in case you want to change the application parameters default values. You can choose any number of the aforementioned `application parameters` in any combination with this command.

  ```PowerShell
  . src\AutoscaleManager\AutoscaleManager\Scripts\Deploy-FabricApplication.ps1 -ApplicationPackagePath 'src\AutoscaleManager\AutoscaleManager\pkg\Release' -PublishProfileFile 'src\AutoscaleManager\AutoscaleManager\PublishProfiles\Cloud.xml' -UseExistingClusterConnection -ApplicationParameter @{ 'NodeManagerActorService_ScanIntervalInSeconds'='120'; 'NodeManagerActorService_ClientOperationTimeoutInSeconds'='120'; 'NodeManagerActorService_DownNodeGraceIntervalInSeconds' = '300'; }
  ```
