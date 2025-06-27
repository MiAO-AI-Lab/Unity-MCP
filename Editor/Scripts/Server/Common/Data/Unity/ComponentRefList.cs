#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using System.ComponentModel;

namespace com.IvanMurzak.ReflectorNet.Model.Unity
{
    [Description(@"Component reference. Used to find Component at GameObject.
Use one of the following properties:
1. 'instanceID' (int) - recommended. It finds the exact Component. Default value is 0.
2. 'index' (int) - finds Component by index. It may find a wrong Component. Default value is -1.
3. 'name' (string) - finds Component by name. It may find a wrong Component. Default value is null.")]
    public class ComponentRefList : List<ComponentRef>
    {
        public ComponentRefList() { }

        public ComponentRefList(int capacity) : base(capacity) { }

        public ComponentRefList(IEnumerable<ComponentRef> collection) : base(collection) { }

        public override string ToString()
        {
            if (Count == 0)
                return "No Components";

            var stringBuilder = new System.Text.StringBuilder();

            stringBuilder.AppendLine($"Components total amount: {Count}");

            for (int i = 0; i < Count; i++)
                stringBuilder.AppendLine($"Component[{i}] {this[i]}");

            return stringBuilder.ToString();
        }
    }
}