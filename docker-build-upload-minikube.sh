docker build hello-service -t skaffold-dotnet-hello:latest 
docker build world-service -t skaffold-dotnet-world:latest
echo Uploading image: skaffold-dotnet-hello to Minikube
minikube image load skaffold-dotnet-hello:latest
echo Uploading image: skaffold-dotnet-world to Minikube
minikube image load skaffold-dotnet-world:latest
echo Done!