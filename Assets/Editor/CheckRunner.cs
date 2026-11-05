using System.Collections;
using System.Collections.Generic;

// Drives an editor check one step per editor tick and, unlike a bare enumerator, also runs nested
// enumerators ("yield return Wait(3f)" really waits). A plain "yield return null" is one tick.
public class CheckRunner
{
    private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

    public CheckRunner(IEnumerator root) { stack.Push(root); }

    // False when the whole check has finished.
    public bool MoveNext()
    {
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
            return true;
        }
        return false;
    }
}
