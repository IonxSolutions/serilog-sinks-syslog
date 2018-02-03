// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Configuration for the behaviour of PeriodicBatchingSink
    /// </summary>
    public class BatchConfig
    {
        public static readonly BatchConfig Default = new BatchConfig(1000, TimeSpan.FromSeconds(2), 100000);

        public int BatchSizeLimit { get; set; }
        public TimeSpan Period { get; set; }
        public int QueueSizeLimit { get; set; }

        public BatchConfig(int batchSizeLimit, TimeSpan period, int queueSizeLimit)
        {
            this.BatchSizeLimit = batchSizeLimit;
            this.Period = period;
            this.QueueSizeLimit = queueSizeLimit;
        }
    }
}
