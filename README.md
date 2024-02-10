# Scalable Hello World in .NET

This repo is a practice on the continuous development concept using [Skaffold](https://skaffold.dev/) involving dummy microservices written in [.NET8](https://dotnet.microsoft.com/en-us/). Skaffold automate the build and deployment process to a local [Kubernetes](https://kubernetes.io/) cluster ([Minikube](https://minikube.sigs.k8s.io/docs/)). 

The objective is to create a scalable monolith. Treat hello-service as main container and world-service as a side-car container. The pod itself, although containing multiple containers, act as a single deployment of a microservice.   

## Kuberenete Concepts

The `kubectl create` command can only create simple resources, it is not use for generating manifest files. In fact we need to create a manifest our own for complex resources and then feed into `kubectl apply` to actually create them on kubernetes cluster. 

The same applies to version release. In a full pipelined environment, each changes in infra (e.g. new image version/tag, or even traffic switch) will commit to repo and trigger build, test and deploy from CI/CD pipeline. In a controlled environment, we can just save the manifest file and execute `kubectl apply` to see the changes and then restore the file etc. 

### High level view
```
+---------------------+
|      Deployment     |
| +-----------------+ |
| |    ReplicaSet   | |
| | +-------------+ | |
| | |     Pod     | | |
| | | +---------+ | | |
| | | |         | | | |
| | | |hello-svc| | | |
| | | |    &    | | | |
| | | |world-svc| | | |
| | | +---------+ | | |
| | +-------------+ | |
| +-----------------+ |
+---------------------+
        |
        |
        |
        v
+----------------+
|    Service     |
+----------------+
```

### Namespace

Namesapce is to isolate resources deployed to the same kubernetes cluster, typical usage is per-tenant, per-project, per-region etc 
```
+--------------------------------------------------------+
|                                                        |
|                  Minikube Kubernetes Cluster           |
|                                                        |
| +--------------------------------------------------+   |
| |                                                  |   |
| |                   Node: minikube-vm              |   |
| |                                                  |   |
| | +----------------------+ +----------------------+ |   |
| | |  Namespace: project  | |  Namespace: project  | |   |
| | |                      | |                      | |   |
| | | +------------------+ | | +------------------+ | |   |
| | | |   Pod (Project   | | | |   Pod (Project   | | |   |
| | | |   A)             | | | |   B)             | | |   |
| | | +------------------+ | | +------------------+ | |   |
| | |                      | |                      | |   |
| | +----------------------+ +----------------------+ |   |
| |                                                  |   |
| +--------------------------------------------------+   |
|                                                        |
|                                                        |
+--------------------------------------------------------+


```

### Replicas (3) and request routing 
```
            +-------------------------------------------+
            |               Kubernetes Service          |
            |                                           |
            |              +---------------+            |
            |              |   Endpoint    |            |
            |              |   Discovery   |            |
            |              +---------------+            |
            |                     |                     |
            |       +-------------|-------------+       |
            |       |             |             |       |
            v       v             v             v       |
  +-------------------+   +-------------------+   +-------------------+
  |     Pod (1)       |   |     Pod (2)       |   |     Pod (3)       |
  |                   |   |                   |   |                   |
  |    Handling       |   |    Handling       |   |    Handling       |
  |    Request        |   |    Request        |   |    Request        |
  |                   |   |                   |   |                   |
  +-------------------+   +-------------------+   +-------------------+

```
- Requests arrive at the Kubernetes Service, which acts as a virtual entry point to the Pods.
- The Service uses Endpoint Discovery to determine which Pods should receive the incoming request.
- Kube-proxy performs load balancing and selects one of the available Pods to handle the request.
- The selected Pod processes the request and sends the response back to the client.

### Relationship between Service & Deployment

```
    +---------------------------+       +---------------------------+
    |       Kubernetes          |       |       Kubernetes          |
    |         Service           |       |        Deployment         |
    +---------------------------+       +---------------------------+
                    |                                       |
              +-----|---------------------------------------|------+
              |     |                                       |      |
              v     v                                       v      v
  +-----------------+   +-----------------+   +-----------------+   +-----------------+
  |     Pod (1)     |   |     Pod (2)     |   |     Pod (3)     |   |     Pod (4)     |
  |                 |   |                 |   |                 |   |                 |
  |    Container    |   |    Container    |   |    Container    |   |    Container    |
  |                 |   |                 |   |                 |   |                 |
  +-----------------+   +-----------------+   +-----------------+   +-----------------+
```

- The Kubernetes Deployment manages multiple Pods, each running an instance of your application.
- Each Pod contains one or more containers, which encapsulate the application logic.
- The Kubernetes Service provides a stable endpoint for accessing the Pods. It abstracts away the details of individual Pod IP addresses and ensures that clients can reliably access the application regardless of which Pod instance is serving the request.

### Ingress, Service & Deployment

```
                   +-------------------+        +-------------------+
                   |      Ingress      |        |      Ingress      |
                   |                   |        |                   |
                   +-------------------+        +-------------------+
                              |                                |
                              |                                |
                              |                                |
                +-------------|--------------+  +--------------|-------------+
                |             |              |  |              |             |
                v             v              v  v              v             v
       +------------------+ +-----------------+ +------------------+ +-----------------+
       |   Deployment     | |     Deployment  | |   Deployment     | |     Deployment  |
       |                  | |                 | |                  | |                 |
       |     Pod (1)      | |     Pod (1)     | |     Pod (1)      | |     Pod (1)     |
       |                  | |                 | |                  | |                 |
       +------------------+ +-----------------+ +------------------+ +-----------------+
                |             |              |  |              |             |
                |             |              |  |              |             |
                v             v              v  v              v             v
       +------------------+ +-----------------+ +------------------+ +-----------------+
       |     Service      | |     Service     | |     Service      | |     Service     |
       |                  | |                 | |                  | |                 |
       |  ClusterIP (Internal Access)         | | LoadBalancer/NodePort (External Access)|
       +------------------+ +-----------------+ +------------------+ +-----------------+
```
- Deployments manage the lifecycle of Pods, ensuring that the desired number of replicas are running.
- Services provide stable endpoints for accessing Pods, allowing intra-cluster communication. They can be of different types, such as ClusterIP for internal access or LoadBalancer/NodePort for external access.
- Ingress resources define rules for routing external HTTP/HTTPS traffic to Services within the cluster based on criteria such as hostnames, paths, or request attributes.

## Skaffold

Now we have the service code in .NET, we have the manifest files and deploy to Kubernetes manually via `kubectl apply` command.
However we need to build and upload docker image to registy before Kubernetes can pull it. Skaffold can automate and bridge the process.
