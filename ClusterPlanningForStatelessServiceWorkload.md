---
author: raunakpandya, vipulm-msft, prashantbhutani90
---

## Guidelines to program Service Fabric cluster for stateless-only workloads
If you use or develop on [Microsoft Azure Service Fabric](https://azure.microsoft.com/services/service-fabric/) platform, you would have wondered how to take benefit of Service Fabric and everything that Azure VMSS has to offer without spiking your devops duty.

This document explains how you can program your Service Fabric cluster for your stateless-only workloads.

- Do not scale primary node types
- Add another node type that is for stateless workload
- Configure autoscaling rules for that new stateless node type
- Deploy the [autoscale manager application](https://github.com/prashantbhutani90/service-fabric-autoscale-helper/tree/stateless) to remove the scaled-in nodes from the cluster