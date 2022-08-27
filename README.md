# Abstract

The MongoDbQueueService it's my vision of what a queue service should be: Light, easy to use and using the new coding standards.

This intension of this project is not replace any comercial queue implementation or vision of queue system, but, it's a theorical and personal vision of how developers should solve the queue problem.

Another goal of this project if to be able to set the full Application Life Cycle since developent, CI/CD and release as nuget package.


# What is included in the Package

There are two available packages:
* MongoDbQueueService
* WorkerUtilitiesService


## MongoDbQueueService

Service responsible to Publish and Subscribe messages into a queue. 


## WorkerUtilitiesService

Service responsible to push and retrieve information about the worker that will process messages from queue.


# How to use MongoDbQueueService

First of all, the worker that will use the MongoDbQueueService will only subscribe to one queue and publish to one queue. 
This is a design decision in order to have a "Light" queue service.
Subscribing different queue should mean have different "workers".

## Configuration file
By convention the configuration file should be called ```appsettings.json``` and will have three (3) possible sections:

* subscriberSettings
This section describes the connection string to the MongoDb and the queue that will be listen.
```
"subscriberSettings": {
    "connectionString": "mongodb://localhost:27017",
    "database": "MongoDatabase",
    "queue": "Queue2Listen",
    "workerName": "WORKER_NAME"
}
```


* publisherSettings
This section describes the connection string to the MongoDb and the follow up queue the worker will publish messages.
```
"publisherSettings": {
    "connectionString": "mongodb://localhost:27017",
    "database": "MongoDatabase",
    "queue": "Queue2Publish" 
}
```


* workerLiveCycleSettings
This section describes the connection string to the MongoDb and the name of the worker
```
"workerLiveCycleSettings": {
    "connectionString": "mongodb://localhost:27017",
    "database": "MongoDatabase",
    "workerName": "WORKER_NAME"
}
```
