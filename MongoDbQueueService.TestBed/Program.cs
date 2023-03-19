// See https://aka.ms/new-console-template for more information
using MongoDbQueueService.Configuration;

/*
var subscriber = new MongoDbQueueService.Subscriber(
    "mongodb://localhost:27017",
    "TestBed",
    "TestQueue",
    "WORKER_1");
*/


/*
var settings = new SubscriberSettings
{
    ConnectionString = "mongodb://localhost:27017",
    Database = "TestBed",
    Queue = "TestQueue",
    WorkerName = "WORKER_1"
};
var subscriber = new MongoDbQueueService.Subscriber(settings);
*/

var subscriber = new MongoDbQueueService.Subscriber(true);

/*
var publisher = new MongoDbQueueService.Publisher(
    "mongodb://localhost:21017",
    "TestBed",
    "TestQueue"
);
*/

/*
var publisherSettings = new PublisherSettings
{
    ConnectionString = "mongodb://localhost:27107",
    Database = "TestBed",
    Queue = "TestQueue"
};
var publisher = new MongoDbQueueService.Publisher(publisherSettings);
*/

var publisher = new MongoDbQueueService.Publisher(true);
