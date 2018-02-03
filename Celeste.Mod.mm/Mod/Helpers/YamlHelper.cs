using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    public static class YamlHelper {

        public static Deserializer Deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        public static Serializer Serializer = new SerializerBuilder().EmitDefaults().Build();

    }
}
