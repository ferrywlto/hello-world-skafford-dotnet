minikube start --nodes=2 && \
minikube addons enable registry && \
minikube addons enable metrics-server && \
minikube addons enable dashboard && \
minikube kubectl apply -- -f config-map.yml && \
minikube kubectl apply -- -f deployment.yml && \
minikube kubectl apply -- -f service.yml && \
minikube dashboard --url