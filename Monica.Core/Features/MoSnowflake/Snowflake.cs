using Monica.Modules;

namespace Monica.Core.Features.MoSnowflake;

/// <summary>
/// Distributed identifier generator based on the Snowflake algorithm.
/// Twitter_Snowflake
/// Snowflake layout, separated here with hyphens for readability:
/// 0 - 0000000000 0000000000 0000000000 0000000000 0 - 00000 - 00000 - 000000000000
/// The highest bit remains zero for positive <see cref="long" /> values.
/// The next 41 bits store the timestamp offset in milliseconds from the configured epoch, which covers about 69 years.
/// The next 10 bits store node information: 5 bits for the datacenter id and 5 bits for the worker id.
/// The final 12 bits store the per-millisecond sequence, allowing up to 4096 identifiers per node per millisecond.
/// This yields a 64-bit, time-ordered identifier with low collision risk across distributed nodes.
/// </summary>
public class Snowflake
{

    // Custom epoch in milliseconds (2015-01-01).
    private readonly long _twepoch;

    // Number of bits assigned to the worker id.
    private readonly int _workerIdBits;

    // Number of bits assigned to the datacenter id.
    private readonly int _datacenterIdBits;

    // Number of bits assigned to the per-millisecond sequence.
    private readonly int _sequenceBits;

    // Left shift applied to the worker id.
    private readonly int _workerIdShift;

    // Left shift applied to the datacenter id.
    private readonly int _datacenterIdShift;

    // Left shift applied to the timestamp offset.
    private readonly int _timestampLeftShift;

    // Bit mask for the sequence portion, for example 4095 when using 12 bits.
    private readonly long _sequenceMask;

    // Worker id in the configured range.
    private readonly long _workerId;

    // Datacenter id in the configured range.
    private readonly long _datacenterId;

    // Sequence number within the current millisecond.
    private long sequence;

    // Timestamp used for the previously generated id.
    private long lastTimestamp = -1L;

    private readonly object _sync = new();

    public Snowflake(
        ModuleSnowflakeIdOption configuration)
    {
        _twepoch = configuration.Twepoch;
        _workerIdBits = configuration.WorkerIdBits;
        _datacenterIdBits = configuration.DatacenterIdBits;
        _sequenceBits = configuration.SequenceBits;
        _workerIdShift = _sequenceBits;

        _datacenterIdShift = _sequenceBits + _workerIdBits;
        _timestampLeftShift = _sequenceBits + _workerIdBits + _datacenterIdBits;
        _sequenceMask = -1L ^ (-1L << _sequenceBits);

        _workerId = configuration.WorkerId;
        _datacenterId = configuration.DatacenterId;

        var maxWorkerId = -1L ^ (-1L << _workerIdBits);
        var maxDatacenterId = -1L ^ (-1L << _datacenterIdBits);

        if (_workerId > maxWorkerId || _workerId < 0)
        {
            throw new ArgumentException(string.Format("worker Id can't be greater than %d or less than 0", maxWorkerId));
        }
        if (_datacenterId > maxDatacenterId || _datacenterId < 0)
        {
            throw new ArgumentException(string.Format("datacenter Id can't be greater than %d or less than 0", maxDatacenterId));
        }
    }



    /// <summary>
    /// Generates the next identifier.
    /// </summary>
    public long NextId()
    {
        lock (_sync)
        {
            var timestamp = TimeGen();

            // Reject clock rollback to preserve monotonic ids.
            if (timestamp < lastTimestamp)
            {
                throw new InvalidTimeZoneException(
                    string.Format("Clock moved backwards.  Refusing to generate id for %d milliseconds", lastTimestamp - timestamp));
            }

            // Same millisecond: advance the in-memory sequence.
            if (lastTimestamp == timestamp)
            {
                sequence = (sequence + 1) & _sequenceMask;
                // Sequence overflow: wait for the next millisecond.
                if (sequence == 0)
                {
                    // Block until a fresh timestamp is available.
                    timestamp = TilNextMillis(lastTimestamp);
                }
            }
            // New millisecond: reset the sequence.
            else
            {
                sequence = 0L;
            }

            // Persist the timestamp used for this id.
            lastTimestamp = timestamp;

            // Assemble the final 64-bit id from the timestamp, datacenter, worker, and sequence parts.
            return ((timestamp - _twepoch) << _timestampLeftShift) //
                   | (_datacenterId << _datacenterIdShift) //
                   | (_workerId << _workerIdShift) //
                   | sequence;
        }
    }




    /// <summary>
    /// Blocks until the clock reaches the next millisecond.
    /// </summary>
    private long TilNextMillis(long lastTimestamp)
    {
        var timestamp = TimeGen();
        while (timestamp <= lastTimestamp)
        {
            timestamp = TimeGen();
        }
        return timestamp;
    }

    /// <summary>
    /// Returns the current UTC time in milliseconds.
    /// </summary>
    protected long TimeGen()
    {
        return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }


}
