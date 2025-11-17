## EventBookingPlatform

## 🚀Overview

This project demonstrates how I build cloud-based, event-driven solutions using ASP.NET Core, React, and several Azure services.
The platform allows users to browse and book events, while the backend processes bookings asynchronously and sends confirmation emails automatically through an Azure Function.
My main goal with this project was to design a scalable, secure, and maintainable application that reflects real-world cloud architecture.

## 🏗️[Architecture]  

  

The solution follows a clean event-driven design:  
The Web API receives booking requests and publishes them to Azure Service Bus.  
An Azure Function App listens to the Service Bus and processes messages independently, sending confirmation emails through SendGrid.  
All secrets (like connection strings and API keys) are stored securely in Azure Key Vault, accessed using Managed Identity.  
Data is stored in Azure Cosmos DB, designed for global scalability and low latency.  
Application Insights is connected to both the Web API and Function App for monitoring and diagnostics.  

## ⚙️[Technologies] 

- Backend: ASP.NET Core 8 (C#), REST APIs  
- Frontend: Next.js, TypeScript  
- Database: Azure Cosmos DB  
- Messaging: Azure Service Bus (Queue integration)  
- Serverless: Azure Function App (triggered by Service Bus)  
- Security: Azure Key Vault, Managed Identity, JWT Authentication  
- Monitoring: Application Insights  
- CI/CD: Azure DevOps Pipelines (YAML-based automation)

## 🌩️[Azure Integration]  

  

Each part of the system runs on Azure and is fully integrated through managed services:  
Service Bus handles communication between independent components without direct dependencies.  
Key Vault manages secrets securely, removing sensitive data from configuration files.  
Function App scales automatically and uses retry policies for reliability.  
Application Insights provides real-time metrics, traces, and logs for troubleshooting and optimization.  


## 🧰[CI/CD Pipeline] 

I implemented a full Azure DevOps pipeline that:  
Builds and publishes both the Web API and Function App.  
Deploys them to Azure App Service and Function App environments.  
Uses environment variables and service connections to keep deployments secure and consistent.  
This setup allows continuous integration and delivery with minimal manual steps.  



## 🧠[What I Focused On and Learned]  


Designing asynchronous integrations using Service Bus and Functions.  
Implementing secure authentication and secret management with Key Vault and Managed Identity.  
Building and automating CI/CD pipelines for cloud deployment.  
Using Application Insights to trace performance and errors in real-time.  
Applying modern cloud development principles aligned with Microsoft Azure AZ-204 concepts.  

