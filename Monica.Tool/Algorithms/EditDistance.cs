namespace Monica.Tool.Algorithms;

public class EditDistance
{
    public enum EditOperationKind : byte
    {
        None,    // Nothing to do
        Add,     // Add new character
        Edit,    // Edit character into character (including char into itself)
        Remove,  // Delete existing character
    };
    public static EditOperation[] EditSequence(string source, string target, int insertCost = 1, int removeCost = 1, int editCost = 2)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(insertCost);
        ArgumentOutOfRangeException.ThrowIfNegative(removeCost);
        ArgumentOutOfRangeException.ThrowIfNegative(editCost);

        // Forward: building score matrix

        // Best operation (among insert, update, delete) to perform 
        var m = Enumerable
          .Range(0, source.Length + 1)
          .Select(line => new EditOperationKind[target.Length + 1])
          .ToArray();

        // Minimum cost so far
        var d = Enumerable
          .Range(0, source.Length + 1)
          .Select(line => new int[target.Length + 1])
          .ToArray();

        // Edge: all removes
        for (var i = 1; i <= source.Length; ++i)
        {
            m[i][0] = EditOperationKind.Remove;
            d[i][0] = removeCost * i;
        }

        // Edge: all inserts 
        for (var i = 1; i <= target.Length; ++i)
        {
            m[0][i] = EditOperationKind.Add;
            d[0][i] = insertCost * i;
        }

        // Having fit N - 1, K - 1 characters let's fit N, K
        for (var i = 1; i <= source.Length; ++i)
            for (var j = 1; j <= target.Length; ++j)
            {
                // here we choose the operation with the least cost
                var insert = d[i][j - 1] + insertCost;
                var delete = d[i - 1][j] + removeCost;
                var edit = d[i - 1][j - 1] + (source[i - 1] == target[j - 1] ? 0 : editCost);

                var min = Math.Min(Math.Min(insert, delete), edit);

                // Prefer a diagonal operation on ties because it produces the shortest,
                // most useful edit script (including equality operations).
                if (min == edit)
                    m[i][j] = EditOperationKind.Edit;
                else if (min == insert)
                    m[i][j] = EditOperationKind.Add;
                else
                    m[i][j] = EditOperationKind.Remove;

                d[i][j] = min;
            }

        // Backward: knowing scores (D) and actions (M) let's building edit sequence
        var result =
          new List<EditOperation>(source.Length + target.Length);

        for (int x = target.Length, y = source.Length; (x > 0) || (y > 0);)
        {
            var op = m[y][x];

            if (op == EditOperationKind.Add)
            {
                x -= 1;
                result.Add(new EditOperation('\0', target[x], op));
            }
            else if (op == EditOperationKind.Remove)
            {
                y -= 1;
                result.Add(new EditOperation(source[y], '\0', op));
            }
            else if (op == EditOperationKind.Edit)
            {
                x -= 1;
                y -= 1;
                result.Add(new EditOperation(source[y], target[x], op));
            }
            else // Start of the matching (EditOperationKind.None)
                break;
        }

        result.Reverse();

        return result.ToArray();
    }
    public readonly struct EditOperation
    {
        public EditOperation(char valueFrom, char valueTo, EditOperationKind operation)
        {
            ValueFrom = valueFrom;
            ValueTo = valueTo;

            Operation = operation == EditOperationKind.Edit && valueFrom == valueTo
                ? EditOperationKind.None
                : operation;
        }

        public char ValueFrom { get; }
        public char ValueTo { get; }
        public EditOperationKind Operation { get; }

        public override string ToString()
        {
            switch (Operation)
            {
                case EditOperationKind.None:
                    return $"'{ValueTo}' Equal";
                case EditOperationKind.Add:
                    return $"'{ValueTo}' Add";
                case EditOperationKind.Remove:
                    return $"'{ValueFrom}' Remove";
                case EditOperationKind.Edit:
                    return $"'{ValueFrom}' to '{ValueTo}' Edit";
                default:
                    return "???";
            }
        }
    }
}
