using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;

namespace Celeste.Mod {
    public static class YamlHelper {

        public static IDeserializer Deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        public static ISerializer Serializer = new SerializerBuilder().EmitDefaults().Build();

        /// <summary>
        /// Builds a deserializer that will provide YamlDotNet with the given object instead of creating a new one.
        /// This will make YamlDotNet update this object when deserializing.
        /// </summary>
        /// <param name="objectToBind">The object to set fields on</param>
        /// <returns>The newly-created deserializer</returns>
        public static IDeserializer DeserializerUsing(object objectToBind) {
            IObjectFactory defaultObjectFactory = new DefaultObjectFactory();
            Type objectType = objectToBind.GetType();

            return new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                // provide the given object if type matches, fall back to default behavior otherwise.
                .WithObjectFactory(type => type == objectType ? objectToBind : defaultObjectFactory.Create(type))
                .Build();
        }

    }
}
