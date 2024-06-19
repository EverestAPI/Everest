#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste {
    class patch_ObjModel : ObjModel {

        private VertexPositionTexture[] verts;

        private object _Vertices_QueuedLoadLock;
        private ValueTask<VertexBuffer> _Vertices_QueuedLoad;

        /// <summary>
        /// Create a new ObjModel from a stream
        /// The filename is mainly just to check if it's a .export
        /// </summary>
        public static ObjModel CreateFromStream(Stream stream, string fname) {
            ObjModel objModel = new ObjModel();
            List<VertexPositionTexture> list = new List<VertexPositionTexture>();
            List<Vector3> list2 = new List<Vector3>();
            List<Vector2> list3 = new List<Vector2>();
            Mesh mesh = null;
            if (fname.EndsWith(".export")) {
                using (BinaryReader binaryReader = new BinaryReader(stream)) {
                    int num = binaryReader.ReadInt32();
                    for (int i = 0; i < num; i++) {
                        if (mesh != null) {
                            mesh.VertexCount = list.Count - mesh.VertexStart;
                        }
                        mesh = new Mesh();
                        mesh.Name = binaryReader.ReadString();
                        mesh.VertexStart = list.Count;
                        objModel.Meshes.Add(mesh);
                        int num2 = binaryReader.ReadInt32();
                        for (int j = 0; j < num2; j++) {
                            float x = binaryReader.ReadSingle();
                            float y = binaryReader.ReadSingle();
                            float z = binaryReader.ReadSingle();
                            list2.Add(new Vector3(x, y, z));
                        }
                        int num3 = binaryReader.ReadInt32();
                        for (int k = 0; k < num3; k++) {
                            float x2 = binaryReader.ReadSingle();
                            float y2 = binaryReader.ReadSingle();
                            list3.Add(new Vector2(x2, y2));
                        }
                        int num4 = binaryReader.ReadInt32();
                        for (int l = 0; l < num4; l++) {
                            int index = binaryReader.ReadInt32() - 1;
                            int index2 = binaryReader.ReadInt32() - 1;
                            list.Add(new VertexPositionTexture {
                                Position = list2[index],
                                TextureCoordinate = list3[index2]
                            });
                        }
                    }
                }
            } else {
                using (StreamReader streamReader = new StreamReader(stream)) {
                    string text;
                    while ((text = streamReader.ReadLine()) != null) {
                        string[] array = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (array.Length != 0) {
                            string a = array[0];
                            if (a == "o") {
                                if (mesh != null) {
                                    mesh.VertexCount = list.Count - mesh.VertexStart;
                                }
                                mesh = new Mesh();
                                mesh.Name = array[1];
                                mesh.VertexStart = list.Count;
                                objModel.Meshes.Add(mesh);
                            } else if (a == "v") {
                                Vector3 item = new Vector3(Float(array[1]), Float(array[2]), Float(array[3]));
                                list2.Add(item);
                            } else if (a == "vt") {
                                Vector2 item2 = new Vector2(Float(array[1]), Float(array[2]));
                                list3.Add(item2);
                            } else if (a == "f") {
                                for (int m = 1; m < Math.Min(4, array.Length); m++) {
                                    VertexPositionTexture item3 = default;
                                    string[] array2 = array[m].Split('/');
                                    if (array2[0].Length > 0) {
                                        item3.Position = list2[int.Parse(array2[0]) - 1];
                                    }
                                    if (array2[1].Length > 0) {
                                        item3.TextureCoordinate = list3[int.Parse(array2[1]) - 1];
                                    }
                                    list.Add(item3);
                                }
                            }
                        }
                    }
                }
            }
            if (mesh != null) {
                mesh.VertexCount = list.Count - mesh.VertexStart;
            }
            ((patch_ObjModel) objModel).verts = list.ToArray();
            ((patch_ObjModel) objModel).ResetVertexBuffer();
            return objModel;
        }

        private extern bool orig_ResetVertexBuffer();
        private bool ResetVertexBuffer() {
            // Checking for IsDisposed on other threads should be fine...
            if (Vertices != null && !Vertices.IsDisposed && !Vertices.GraphicsDevice.IsDisposed)
                return false;

            // Handle already queued loads appropriately.
            object queuedLoadLock = _Vertices_QueuedLoadLock;
            if (queuedLoadLock != null) {
                lock (queuedLoadLock) {
                    // Queued task finished just in time.
                    if (_Vertices_QueuedLoadLock == null)
                        return true;

                    // If we still can, cancel the queued load, then proceed with lazy-loading.
                    if (MainThreadHelper.IsMainThread)
                        _Vertices_QueuedLoadLock = null;
                }

                if (!MainThreadHelper.IsMainThread) {
                    // Otherwise wait for it to get loaded, don't reload twice. (Don't wait locked!)
                    _ = _Vertices_QueuedLoad.Result;
                    return true;
                }
            }

            if (!(CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL) && !MainThreadHelper.IsMainThread && queuedLoadLock == null) {
                // Let's queue a reload onto the main thread and call it a day.
                lock (queuedLoadLock = new object()) {
                    _Vertices_QueuedLoadLock = queuedLoadLock;
                    _Vertices_QueuedLoad = MainThreadHelper.Schedule(() => {
                        lock (queuedLoadLock) {
                            if (_Vertices_QueuedLoadLock == null)
                                return Vertices;
                            // Force-reload as we already returned true on the other thread.
                            Vertices?.Dispose();
                            // NOTE: If something dares to change verts on the fly, make it wait on any existing tasks, then make it force-reload.
                            Vertices = new VertexBuffer(Engine.Graphics.GraphicsDevice, typeof(VertexPositionTexture), verts.Length, BufferUsage.None);
                            Vertices.SetData(verts);
                            _Vertices_QueuedLoadLock = null;
                            return Vertices;
                        }
                    });
                }
                return true;
            }

            return orig_ResetVertexBuffer();
        }

        [MonoModIgnore]
        private static extern float Float(string data);

    }

    public static class ObjModelExt {

        /// <inheritdoc cref="patch_ObjModel.CreateFromStream(Stream, string)"/>
        [Obsolete("Use ObjModel.CreateFromStream instead.")]
        public static ObjModel CreateFromStream(Stream stream, string fname) {
            return patch_ObjModel.CreateFromStream(stream, fname);
        }

    }
}
