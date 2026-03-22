# Telegram killer

Chat API made using ASP.NET Core + SignalR. Allows sending plain texts
and marking sent messages as read in real-time. 

# Tech stack
* ASP.NET Core MVC
* Entity Framework Core with PostgreSQL provider
* Serilog + Seq sink
* OpenAPI + Scalar UI
* [AsyncAPI html template](https://github.com/asyncapi/html-template)

# Using

## Prerequisites

1. .NET 9 SDK
2. Database access(PostgreSQL provider)

## Clone the repository

```bash
git clone https://github.com/blendereru/telegram-killer.API.git
cd telegram-killer.API
```

## Make sure your variables are configured
Update configuration variables in [appsettings](telegram-killer.API/appsettings.json):

```json
"SecuritySettings": {
    "ConfirmationCodeSecret": "YOUR_SECRET" // consumed when storing confirmation code hash in db
},
"EmailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "YOUR_USERNAME",
    "Password": "YOUR_PASSWORD",
    "FromEmail": "YOUR_FROM_EMAIL",
    "FromName": "YOUR_FROM_NAME"
},
"JwtConfigurationOptions": {
    "Audience": "telegram-killer-audience",
    "Issuer": "Sanzhar",
    "Lifetime": 30,
    "Key": "mysupersecret_secretsecretsecretkey!123"
},
"ConnectionStrings": {
    "DefaultConnection": "YOUR_CONNECTION_STRING"
}
```

## Run the app

```bash
dotnet run
```

The API is then available at:
http://localhost:5196

## Checks the docs

* API documentation is available by `/scalar` route
* WebSockets documentation is generated via AsyncAPI and is available by `/index.html`

## Run tests
If you are at web app level go one level down and launch tests

```bash
cd ..
dotnet test
```

# Using docker
Use the following command:

```bash
docker compose up --build
```

And access api by http://localhost:8080

# License

The project is licensed under [MIT](LICENSE.txt)
