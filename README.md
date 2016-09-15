# A Service Fabric Sidecar Service
In a microservice architecture a sidecar is a service that is tightly coupled to another service and extend that service with regards to functionality. A sidecar service is often used to couple service and client libraries written in different programming languages. The [Netflix Prana](http://techblog.netflix.com/2014/11/prana-sidecar-for-your-netflix-paas.html) sidecar is an example of such a service. In the container context the [Kubernetes blog](http://blog.kubernetes.io/2015/06/the-distributed-system-toolkit-patterns.html) provides a nice overview on the different concepts of sidecar, ambassador (see also the Docker documentation ["link via an ambassador container"](https://docs.docker.com/engine/admin/ambassador_pattern_linking/)), and adapter containers. Following that line of thought an ambassador service proxies request to the underlying service which allows that service to be replaced or restarted without interruption. 

The sidecar service discussed here both extends functionality and acts as a proxy for multiple underlying services, hence it has trades from both the sidecar and ambassador patterns. For simplicity we call it a sidecar service.

The Service Fabric sidecar service enables external services to be accessed from within the Service Fabric. The sidecar proxies requests and can be looked up using the naming service in Service Fabric. Combining the sidecar service with a reverse proxy makes the external service available through the Service Fabric by going through the reverse proxy and the sidecar. This potentially eliminates the need for other service discovery methods.

Additionally, the sidecar service performs health checks on the external services and reports them to the Service Fabric health store. This allows monitoring the health of external services using the Service Fabric health model and allows us to use only one place for monitoring the health of all services in the system.

The sidecar allows the incorporation of services in different languages, services on other platforms, or third-party services into the Service Fabric cluster. It allows one to gradually move legacy services into the Service Fabric cluster at ones own pace and take advantage of the service discovery and health monitor capabilities of Service Fabric right away. In general, a sidecar service provides a great deal of flexibility to a microservice architecture although it also adds an additional failure point, complexity, and increased network traffic.

## Getting Started
In this guide we will use a [local cluster](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-get-started-with-a-local-cluster/#create-a-local-cluster) but the same applies for hosted or on-premise clusters. Substitute the `localhost` with the correct cluster endpoint if not running on a local cluster. To deploy the sidecar application type to make it available in the Service Fabric cluster we follow the walkthrough found in the [documentation](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-deploy-remove-applications/).

Open the sidecar solution and package the application. In Visual Studio, right-click on the `Sidecar` project and select 'package'. This produces a folder in your project folder like `pkg\<Build Configuration>`. Rename the folder to the name of the application type `pkg\SidecarType`.
```
PS> mv <Build Configuration> SidecarType
```   

### Preliminaries
Now, connect to the cluster
```
PS> Connect-ServiceFabricCluster -ConnectionEndpoint "localhost:19000"
```
 and import the SDK module
```
PS> Import-Module "$ENV:ProgramFiles\Microsoft SDKs\Service Fabric\Tools\PSModule\ServiceFabricSDK\ServiceFabricSDK.psm1"
```

### Register the Sidecar Service
Navigate to the `pkg` folder and copy the package to the image store of the cluster
```
PS> Copy-ServiceFabricApplicationPackage -ApplicationPackagePath SidecarType -ImageStoreConnectionString (Get-ImageStoreConnectionStringFromClusterManifest(Get-ServiceFabricClusterManifest))
```
Then register the application type with the fabric
```
PS> Register-ServiceFabricApplicationType SidecarType
```
Now the `SidecarType` is a available as an application type and can be used as a basis for deploying sidecar applications and thereby sidecar services. Run
```
PS> Get-ServiceFabricApplicationType -ApplicationTypeName SidecarType


ApplicationTypeName    : SidecarType
ApplicationTypeVersion : 1.0.0
DefaultParameters      : { "SidecarService_InstanceCount" = "-1";
                         "Sidecar_DoHealthCheck" = "True";
                         "Sidecar_Endpoints" = "" }


```
to verify that the `ServiceType` application type has been registered correctly.

### Start External Services
Start up some external services. First build the `MyExternalService` solution and start up a few instances with the port number as the argument 
```
PS> Start-Process MyExternalService.exe 9000
PS> Start-Process MyExternalService.exe 9001
```
Verify that the services are running. For instance ping a service and get a health status
```
PS> Invoke-WebRequest http://localhost:9000/ping | Select Content

Content
-------
"Endpoint: 127.0.0.1:9000"


PS> Invoke-WebRequest http://localhost:9000/health | Select Content

Content
-------
true


```
### Start the Sidecar Service
We denote the external application `MyExternalApp` and the service names as `MyExternalService`. To make them available in Service Fabric we first create a new application  
```
PS> New-ServiceFabricApplication -ApplicationName fabric:/MyExternalApp -ApplicationTypeName "SidecarType" -ApplicationTypeVersion "1.0.0" -ApplicationParameter @{"Sidecar_Endpoints"="http://localhost:9000,http://localhost:9001"}
```
and then start the sidecar service
```
PS> New-ServiceFabricService -ApplicationName fabric:/MyExternalApp -ServiceName fabric:/MyExternalApp/MyExternalService -ServiceTypeName SidecarServiceType -Stateless -PartitionSchemeSingleton -InstanceCount 1
```
Now the external services can be reached from within Service Fabric by using the naming service requesting the url `fabric:/MyExternalApp/MyExternalService`. The sidecar service proxies request to the external services using a random selection between available urls.

Verify that everything went well
```
PS>  Get-ServiceFabricService -ApplicationName fabric:/MyExternalApp


ServiceName            : fabric:/MyExternalApp/MyExternalService
ServiceKind            : Stateless
ServiceTypeName        : SidecarServiceType
IsServiceGroup         : False
ServiceManifestVersion : 1.0.0
ServiceStatus          : Active
HealthState            : Ok


```
Now we can reach the external service through the sidecar service (it exposes port 8130). It may look like this
```
PS> Invoke-WebRequest http://localhost:8130/ping | Select Content

Content
-------
"Endpoint: 127.0.0.1:9000"


PS> Invoke-WebRequest http://localhost:8130/ping | Select Content

Content
-------
"Endpoint: 127.0.0.1:9001"


```
Note that we randomly reach different endpoints of the external service.

In this basic implementation of the sidecar it is not supported to add more external endpoints dynamically. One would need to redeploy the application with the new parameter setting including the new endpoints.

## Health Checks
The sidecar additionally implements a health check of the external service at route `<service endpoint>/health`. The sidecar service periodically calls the health endpoint and sends a health report being sent to the Service Fabric [health store](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-health-introduction). Requests with successful response codes result in positive health reports and unsuccessful response codes or time outs result in error reports. 

The aggregated health report of the sidecar service will show errors if one of the external service endpoints is not healthy.

Check the aggregated health report of the sidecar application
```
PS> Get-ServiceFabricApplicationHealth -ApplicationName fabric:/MyExternalApp | Select -ExpandProperty ServiceHealthStates

ServiceName                             AggregatedHealthState
-----------                             ---------------------
fabric:/MyExternalApp/MyExternalService                    Ok


```
Then check on the health on the lowest level, the replica,

```
PS> Get-ServiceFabricPartition -ServiceName fabric:/MyExternalApp/MyExternalService | Get-ServiceFabricReplica | Get-ServiceFabricReplicaHealth | Select -ExpandProperty HealthEvents | Select -ExpandProperty HealthInformation | Select SourceId, HealthState

SourceId              HealthState
--------              -----------
System.RA                      Ok
http://localhost:9000          Ok
http://localhost:9001          Ok


```
Notice that the endpoints are added as `SourceId` in the health report. The aggregated health model of Service Fabric ensures that if either of the sources on the replica level is unhealthy then the above levels also become unhealthy.

Close an external service by closing the window or stop both by running
```
PS> Stop-Process -Name "MyExternalService.exe"
```
Retrieve the health report on the replica as above and verify that the it is now unhealthy. The `HealthState` is set to `Error` for the external services that are closed. Looking at the aggregated report for the replica could be like this
```
PS> Get-ServiceFabricPartition -ServiceName fabric:/MyExternalApp/MyExternalService | Get-ServiceFabricReplica | Get-ServiceFabricReplicaHealth | Select AggregatedHealthState, UnhealthyEvaluations

AggregatedHealthState UnhealthyEvaluations
--------------------- --------------------
                Error {HealthState: Error: Error event: SourceId='http://localhost:9000', Property='Health'.}


```
On application level the aggregated health report also has `AggregatedHealthState` set to `Error`. Turn the external service on again and observe that the health is restored after a while.


The health checks can be disabled when deploying the application by passing the `"Sidecar_DoHealthCheck"="False"` parameter in the `-ApplicationParameter` array.  

## Adding a Reverse Proxy
It is possible to leverage the naming service in Service Fabric and make the sidecar available for other external services through a reverse proxy. Then external services need only to know about the reverse proxy of the Service Fabric cluster. Se my other [post]() or [github repository]() for details on deploying a simple reverse proxy to a Service Fabric cluster.

After deploying the reverse fabric we can test the functionality by calling
```
PS> Invoke-WebRequest http://localhost:8080/MyExternalApp/MyExternalService/  | Select Content

Content
-------
"Endpoint: 127.0.0.1:9001"


```
Recall that the sidecar service currently forwards request randomly between external endpoints. Hence, even though the reverse proxy uses round-robin to connect to sidecar services the external endpoint is in fact picked at random. 

Using the sidecar service pattern for external services together with a reverse proxy simplifies the use of the naming service in Service Fabric for service discovery. Moreover, it provides a unified approach for communication between services in the Service Fabric cluster and outside.