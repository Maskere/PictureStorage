echo "Deploying to folder publish"
rm -rf publish/ && dotnet publish -o publish/ -r linux-arm64 --self-contained true

echo "Restarting the service"
sudo systemctl restart picture-api.service
