# Scalable Hello World in .NET

This repo is a practice on the continuous development concept using [Skaffold](https://skaffold.dev/) involving dummy microservices written in [.NET8](https://dotnet.microsoft.com/en-us/). Skaffold automate the build and deployment process to a local [Kubernetes](https://kubernetes.io/) cluster ([Minikube](https://minikube.sigs.k8s.io/docs/)). 

## Kuberenete Concepts

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