using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDbQueueService
{
    public class QueueCollection
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public BsonDocument Payload { get; set; }

        public string WorkerName { get; set; }

        public int Priority { get; set; }

        public DateTime LastTimeChanged { get; set; }

        public bool Processed { get; set; }
    }
}
