using NLua;
using NLua.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    public class LuaCoroutine : IEnumerator {

        public readonly LuaTable Proxy;

        private object _Current;
        private bool _Valid;

        public object Current => _Valid ? _Current : throw new InvalidOperationException();

        public LuaCoroutine(LuaTable proxy) {
            Proxy = proxy;
        }

        public bool MoveNext() {
            object[] rva = (Proxy["resume"] as LuaFunction)?.Call(Proxy);
            if (_Valid = (bool) rva[0]) {
                _Current = rva.ElementAtOrDefault(1);
                return true;
            } else {
                _Current = null;
                if (rva.Length == 2 && rva[1] is Exception e)
                    throw e;
                return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

    }
}
