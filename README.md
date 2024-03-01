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
In Minikube's docker engine repository, the default images are:
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

## Multi-node Minikube Setup

In order to simulate production environment, it is better to have at least two nodes up and running to view how the pods are assigned/scheduled to nodes.

However using the approach in [previous section](#kickstarting-without-skaffold) will not work for multi-node cluster.

This is because the pods across nodes not able to discover and resolve the image.

Running `minikube docker-env` on a multi-node cluster will throw the error below:
```
ferry@MBP hello-world-skafford-dotnet % minikube docker-env

❌  Exiting due to ENV_MULTINODE_CONFLICT: The docker-env command is incompatible with multi-node clusters. Use the 'registry' add-on: https://minikube.sigs.k8s.io/docs/handbook/registry/
```

We need to setup a docker image registry in the minikube cluster. In production since the images are probably pushed to DockerHub / GitHub Private Registry / Google Artifacts Registry etc, we don't need to setup our own registry. 

### Enable Minikube Registry Addon

In case things not working, delete the cluster and start all over to ensure things are clean and fresh.

- Run `minikube addon enable registry` for existing cluster
- Run `minikube start --addons registry` for new cluster

```
💡  registry is an addon maintained by minikube. For any concerns contact minikube on GitHub.
You can view the list of minikube maintainers at: https://github.com/kubernetes/minikube/blob/master/OWNERS
╭──────────────────────────────────────────────────────────────────────────────────────────────────────╮
│                                                                                                      │
│    Registry addon with docker driver uses port 62810 please use that instead of default port 5000    │
│                                                                                                      │
╰──────────────────────────────────────────────────────────────────────────────────────────────────────╯
📘  For more information see: https://minikube.sigs.k8s.io/docs/drivers/docker
    ▪ Using image docker.io/registry:2.8.3
    ▪ Using image gcr.io/k8s-minikube/kube-registry-proxy:0.0.5
🔎  Verifying registry addon...
🌟  The 'registry' addon is enabled
```

Ignore the port prompt, it is not related.

### Verify the registry addon is running 

Verify the registry and registry proxy pods are running. Note that there is only one registry pod for the whole cluster and a registry proxy per each node.

This is because the registry pod was defined as a replication controller. While the registry proxy pods was defined as deamon sets.

When receiving an image pull request when creating pods, if the request specify a specific registry `localhost:5000/your-app-image`, the registry proxy will intercept the request and forward to the registry service (see below).  Therefore when we create a deployment, make sure to specify the special `localhost:5000/` registry address in `image` field in manifest.  

- Deployment Manifest
```
image: localhost:5000/skaffold-dotnet-hello:latest
```
- Pods
```
ferry@MBP hello-world-skafford-dotnet % minikube kubectl get pod -- -o wide --namespace kube-system
NAME                               READY   STATUS    RESTARTS      AGE   IP             NODE           NOMINATED NODE   READINESS GATES
registry-cnlbc                     1/1     Running   4 (16m ago)   16m   10.244.0.2     minikube       <none>           <none>
registry-proxy-4mzjg               1/1     Running   1             16m   10.244.0.5     minikube       <none>           <none>
registry-proxy-t7j8z               1/1     Running   1 (15m ago)   16m   10.244.1.2     minikube-m02   <none>           <none>
```
- Replication Controller
```
ferry@MBP hello-world-skafford-dotnet % minikube kubectl get rc -- -o wide --namespace kube-system | grep registry
NAME       DESIRED   CURRENT   READY   AGE   CONTAINERS   IMAGES                     SELECTOR
registry   1         1         1       29m   registry     docker.io/registry:2.8.3   kubernetes.io/minikube-addons=registry
```
- Deamon Sets
```
ferry@MBP hello-world-skafford-dotnet % minikube kubectl get ds -- -o wide --namespace kube-system
NAME             DESIRED   CURRENT   READY   UP-TO-DATE   AVAILABLE   NODE SELECTOR   AGE   CONTAINERS       IMAGES                                    SELECTOR
registry-proxy   2         2         2       2            2           <none>          31m   registry-proxy   gcr.io/k8s-minikube/kube-registry-proxy   kubernetes.io/minikube-addons=registry,registry-proxy=true
```
- Service
```
ferry@MBP hello-world-skafford-dotnet % minikube kubectl get svc -- -o wide --namespace kube-system
NAME             TYPE        CLUSTER-IP       EXTERNAL-IP   PORT(S)                  AGE   SELECTOR
registry         ClusterIP   10.109.156.241   <none>        80/TCP,443/TCP           34m   actual-registry=true,kubernetes.io/minikube-addons=registry
```

Up to this point the images are still not able to pull yet. We need to push the image. 

### Pushing image to registry service on Minikube

- Create a port forwarding before push. Note that the service port is 80, not 5000. 

```
ferry@MBP hello-world-skafford-dotnet % minikube kubectl port-forward -- service/registry 5000:80 --namespace=kube-system
Forwarding from 127.0.0.1:5000 -> 5000
Forwarding from [::1]:5000 -> 5000
Handling connection for 5000
```

- Verify the port-forwarding success. 
```
ferry@MBP hello-world-skafford-dotnet % curl http://localhost:5000/v2/_catalog
{"repositories":[]}
```

- If the `curl` get a 403 response, this is because on MacOS the AirPlay service listen to port 5000 either, have to disable it as specified in [Apple Developer Forum](https://forums.developer.apple.com/forums/thread/693768).
```
**HTTP/1.1 403 Forbidden
Content-Length: 0
Server: AirTunes/595.13.1**
```

- Tag and push the image
```
ferry@MBP hello-world-skafford-dotnet % docker tag skaffold-dotnet-hello:latest localhost:5000/skaffold-dotnet-hello:latest
ferry@MBP hello-world-skafford-dotnet % docker push localhost:5000/skaffold-dotnet-hello:latest
The push refers to repository [localhost:5000/skaffold-dotnet-hello]
Get "http://localhost:5000/v2/": dial tcp [::1]:5000: connect: connection refused
```

It still doesn't working.

- **One more traffic redirection needed as told by [Minikube Handbook - Registry](https://minikube.sigs.k8s.io/docs/handbook/registry/), because of authentication and TLS stuff.**

```
docker run --rm -it --network=host alpine ash -c "apk add socat && socat TCP-LISTEN:5000,reuseaddr,fork TCP:$(minikube ip):5000"
```

The command sets up a temporary Alpine Linux container with the socat tool to forward traffic from port 5000 on the host machine to port 5000 on a Minikube VM.

```
ferry@MBP hello-world-skafford-dotnet % docker run --rm -it --network=host alpine ash -c "apk add socat && socat TCP-LISTEN:5000,reuseaddr,fork TCP:host.docker.internal:5000"

fetch https://dl-cdn.alpinelinux.org/alpine/v3.19/main/aarch64/APKINDEX.tar.gz
fetch https://dl-cdn.alpinelinux.org/alpine/v3.19/community/aarch64/APKINDEX.tar.gz
(1/4) Installing ncurses-terminfo-base (6.4_p20231125-r0)
(2/4) Installing libncursesw (6.4_p20231125-r0)
(3/4) Installing readline (8.2.1-r2)
(4/4) Installing socat (1.8.0.0-r0)
Executing busybox-1.36.1-r15.trigger
OK: 9 MiB in 19 packages
```

Try to push again and worked.

```
ferry@MBP hello-world-skafford-dotnet % docker push localhost:5000/skaffold-dotnet-hello:latest
The push refers to repository [localhost:5000/skaffold-dotnet-hello]
640f1a35ef85: Pushed 
63ce94ea28ea: Pushed 
e63ae026ff70: Pushed 
61d98a5b18b3: Pushed 
39e09b8de378: Pushed 
4956fd172831: Pushed 
7c504f21be85: Pushed 
latest: digest: sha256:52dbca1de164b9fb729e4589192355dbfd410d13981856e98b0e47df1443464d size: 1787
```

Not really sure why port-forwarding is not working but socat works. Append ChatGPT result for future reference.

> kubectl port-forward and socat are two different methods of forwarding network traffic, and they have different characteristics that can affect how they work with different types of traffic.
>
> kubectl port-forward forwards traffic from a local port to a port on a Kubernetes pod. It's a simple way to access a service running on a pod from your local machine. However, it's designed for interactive use and may not handle all types of network traffic correctly. In particular, it may have issues with HTTP/2 traffic, which Docker uses for pushing images.
>
> On the other hand, socat is a more general-purpose network utility that can forward almost any type of network traffic. It's more complex to set up than kubectl port-forward, but it can handle a wider range of network protocols and traffic patterns.
>
> When you use socat to forward traffic to a Docker registry, it's able to handle the HTTP/2 traffic used by Docker push operations. However, depending on how you've set it up, it may not be correctly forwarding HTTP traffic from your browser.

## Accessing the app without Ingress

### Accessing the containers by service

By default the services defined and the corresponding containers inside pods are not accessible from outside of cluster.

If service type is `NodePort`:

Create port-forwarding with `minikube service skaffold-dotnet`. 
Under the hood it run `kubectl port forward`.

```
ferry@MBP hello-world-skafford-dotnet % minikube service skaffold-dotnet
|-----------|-----------------|-------------|---------------------------|
| NAMESPACE |      NAME       | TARGET PORT |            URL            |
|-----------|-----------------|-------------|---------------------------|
| default   | skaffold-dotnet |        8082 | http://192.168.58.2:31888 |
|-----------|-----------------|-------------|---------------------------|
🏃  Starting tunnel for service skaffold-dotnet.
|-----------|-----------------|-------------|------------------------|
| NAMESPACE |      NAME       | TARGET PORT |          URL           |
|-----------|-----------------|-------------|------------------------|
| default   | skaffold-dotnet |             | http://127.0.0.1:60271 |
|-----------|-----------------|-------------|------------------------|
🎉  Opening service default/skaffold-dotnet in default browser...
❗  Because you are using a Docker driver on darwin, the terminal needs to be open to run it.
✋  Stopping tunnel for service skaffold-dotnet.
```

If service type is `LoadBalancer`:

Creating tunnelling by `miuikube tunnel`:
```
ferryw@MBP hello-world-skafford-dotnet % minikube tunnel      
✅  Tunnel successfully started

📌  NOTE: Please do not close this terminal as this process must stay alive for the tunnel to be accessible ...

🏃  Starting tunnel for service skaffold-dotnet.
```

Get exposed port from: `kubectl get svc`
```
ferry@MBP hello-world-skafford-dotnet % kubectl get svc
NAME              TYPE           CLUSTER-IP       EXTERNAL-IP   PORT(S)          AGE
kubernetes        ClusterIP      10.96.0.1        <none>        443/TCP          4d3h
skaffold-dotnet   LoadBalancer   10.101.225.104   127.0.0.1     8082:31888/TCP   27h
```

### Accessing the containers by port forwarding

To directly access particular pod: ` kubectl port-forward <pod_name> <host_port>:<container_port>`
```
ferry@MBP hello-world-skafford-dotnet % kubectl port-forward skaffold-dotnet-7f55cf7c69-n4w4d 5002:5002
Forwarding from 127.0.0.1:5002 -> 5002
Forwarding from [::1]:5002 -> 5002
Handling connection for 5002
Handling connection for 5002
```

### Expose the service to the Internet

After tunnel created, use [Ngrok](https://ngrok.com/docs/using-ngrok-with/docker/) to bridge the Internet to cluster.

`docker run -it -e NGROK_AUTHTOKEN=xyz ngrok/ngrok:latest http host.docker.internal:<exposed_port>`

```
docker run -it -e NGROK_AUTHTOKEN=2GGl7DqTPWxd9X7veEKaEm5aigT_7wx1onmg1DgET2LdA8Bhq ngrok/ngrok:latest http host.docker.internal:8082
Forwarding  https://decb-173-244-49-57.ngrok-free.app -> http://host.docker.internal:8082 
```

We should able to call the service from the Internet now.

## ConfigMaps

If a config map is exposed as environment variable, then in code we read as key-value pair

```
From env:
- foo: bar
- which_config: env
```

Config as environment variables changes won't reflect to pods until pod restart/recreate.


If a config map is mounted as a volume, then each config value is a file with the key as file name and value as file content.

From mounted volume:
- file: bar
- content: foo
- file: which_config
- content: mount
```

Mounted config changes will reflect to pods. 

To prevent someone else accidentally updated the config values, set `immutable: true`

Attempting to do so will see this error when `kubectl apply`:
```
The ConfigMap "skaffold-dotnet-configmap-mount-immutable" is invalid: data: Forbidden: field is immutable when `immutable` is set
```

## Secrets

If a secret was created in YAML manifest, the data need to be base64 encoded. Otherwise we will see below error when we `kubectl apply`:

```
Error from server (BadRequest): error when creating "secrets.yml": Secret in version "v1" cannot be handled as a Secret: illegal base64 data at input byte 5
```

On MacOS we can encode a string to base64 by: `echo "your_secret" | base64`


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
