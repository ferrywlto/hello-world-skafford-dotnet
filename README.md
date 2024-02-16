# Scalable Hello World in .NET

This repo is a practice on the continuous development concept using [Skaffold](https://skaffold.dev/) involving dummy microservices written in [.NET8](https://dotnet.microsoft.com/en-us/). Skaffold automate the build and deployment process to a local [Kubernetes](https://kubernetes.io/) cluster ([Minikube](https://minikube.sigs.k8s.io/docs/)). 

The objective is to create a scalable monolith. Treat hello-service as main container and world-service as a side-car container. The pod itself, although containing multiple containers, act as a single deployment of a microservice.   

In each pod there will be 3 containers:
- Nginx that host the frontend static web-page
- .NET8 API server
- MongoDB

## APIs

There will be two REST API available:

### `GET /hello/name`

This API will get the list of who sent hello to the specified `name` since last query.

### `POST /hello/name`

This API will send hello to recipient `name`.


## Kickstarting without Skaffold

1. We need to build docker image by our own first
```
// A script created for the build task
./docker-build-upload-minikube.sh
```

2. Ensure the built images are loaded into Minikube's docker registry
To view Minikube's docker registry:
```
// this will point current shell to Minibuke's docker deamon
eval $(minikube -p minikube docker-env)
docker images
```
In Minikube's docker image registry, the default images are:
```
REPOSITORY                                      TAG       IMAGE ID       CREATED         SIZE
registry.k8s.io/kube-scheduler                  v1.28.3   42a4e73724da   3 months ago    57.8MB
registry.k8s.io/kube-controller-manager         v1.28.3   8276439b4f23   3 months ago    116MB
registry.k8s.io/kube-apiserver                  v1.28.3   537e9a59ee2f   3 months ago    120MB
registry.k8s.io/kube-proxy                      v1.28.3   a5dd5cdd6d3e   3 months ago    68.3MB
registry.k8s.io/metrics-server/metrics-server   <none>    24087ab2d904   6 months ago    66.9MB
registry.k8s.io/etcd                            3.5.9-0   9cdd6470f48c   9 months ago    181MB
registry.k8s.io/coredns/coredns                 v1.10.1   97e04611ad43   12 months ago   51.4MB
registry.k8s.io/pause                           3.9       829e9de338bd   16 months ago   514kB
kubernetesui/dashboard                          <none>    20b332c9a70d   17 months ago   244MB
kubernetesui/metrics-scraper                    <none>    a422e0e98235   20 months ago   42.3MB
gcr.io/k8s-minikube/storage-provisioner         v5        ba04bb24b957   2 years ago     29MB
```

// this command is IMPORTANT, otherwise kubernetes cannot pull the images
```shell
minikube image load skaffold-dotnet-hello:latest 
minikube image load skaffold-dotnet-world:latest
``` 

3. Ensure manifest has `imagePullPolicy` set to `Never`, otherwise Minikube will try to pull image from Docker Hub registry
```yaml
containers:
  - name: hello-containers
    image: skaffold-dotnet-hello:latest
    imagePullPolicy: Never
```




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

## Pod Scheduling

Means when a pod created, the node assignment mechanism and criteria by Scheduler. Optimize resource utilization.

## Skaffold

Now we have the service code in .NET, we have the manifest files and deploy to Kubernetes manually via `kubectl apply` command.
However we need to build and upload docker image to registy before Kubernetes can pull it. Skaffold can automate and bridge the process.
