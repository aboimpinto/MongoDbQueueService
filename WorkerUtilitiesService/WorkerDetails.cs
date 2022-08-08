using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WorkerUtilitiesService
{
    public class WorkerDetails
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public Guid WorkerInternalId { get; set; }

        public string WorkerName { get; set; }

        public DateTime LastOperationTimeStamp { get; set; }

        public string LastOperationMessage { get; set; }
    }
}
