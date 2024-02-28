docker build hello-service -t skaffold-dotnet-hello:latest 
docker build world-service -t skaffold-dotnet-world:latest
echo Uploading image: skaffold-dotnet-hello to Minikube
minikube image load skaffold-dotnet-hello:latest
echo Uploading image: skaffold-dotnet-world to Minikube
minikube image load skaffold-dotnet-world:latest
echo Done!


docker compose build
docker tag skaffold-dotnet-world:latest localhost:5000/skaffold-dotnet-world:v0.0.3

# ensure registry is up
minikube service registry --namespace kube-system

# before can push
docker run --rm -it --network=host alpine ash -c "apk add socat && socat TCP-LISTEN:5000,reuseaddr,fork TCP:$(minikube ip):5000"

# push image to registry
docker push localhost:5000/skaffold-dotnet-world:v0.0.3

# update deployment.yml to use new image version first then rollout changes
minikube kubectl apply -- -f deployment.yml

# before accessing the service from browser
minikube tunnel