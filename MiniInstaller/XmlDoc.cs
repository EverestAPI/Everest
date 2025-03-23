using System.IO;
using System.Xml;

namespace MiniInstaller;

public static class XmlDoc {
    public static void CombineXMLDoc(string xmlFrom, string xmlTo) {
        Logger.LogLine("Combining Documentation");
        XmlDocument from = new XmlDocument();
        XmlDocument to = new XmlDocument();

        // Not worth crashing over.
        try {
            from.Load(xmlFrom);
            to.Load(xmlTo);
        } catch (FileNotFoundException e) {
            Logger.LogLine(e.Message);
            Logger.LogErr("Documentation combining aborted.");
            return;
        }

        XmlNodeList members = from.DocumentElement.LastChild.ChildNodes;

        // Reverse for loop so that we can remove nodes without breaking everything
        for (int i = members.Count - 1; i >= 0; i--) {
            XmlNode node = members[i];
            XmlAttribute name = node.Attributes["name"];
            string noPatch = name.Value.Replace("patch_", "");
            if (!noPatch.Equals(name.Value)) {
                // Remove internal inheritdoc members that would otherwise override "vanilla" celeste members.
                if (node.SelectNodes($"inheritdoc[@cref='{noPatch}']").Count == 1) {
                    node.ParentNode.RemoveChild(node);
                    continue;
                }
                name.Value = noPatch;
            }

            // Fix up any references to patch_ class members.
            foreach (XmlAttribute cref in node.SelectNodes(".//@cref"))
                cref.Value = cref.Value.Replace("patch_", "");

            // I couldn't find a way to just do this for all orig_ methods, so an <origdoc/> tag needs to be manually added to them.
            // And of course there also doesn't seem to be support for adding custom tags to the xmldoc prompts -_-
            if (node.ChildNodes.Count == 1 && node.FirstChild.LocalName.Equals("origdoc")) {
                XmlNode origDoc = from.CreateElement("summary");
                CreateOrigDoc(node.FirstChild, ref origDoc);
                node.RemoveChild(node.FirstChild);
                node.AppendChild(origDoc);
            }
        }

        // Remove any pre-existing Everest docs
        members = to.DocumentElement.ChildNodes;
        for (int i = members.Count - 1; i >= 0; i--) {
            XmlNode node = members[i];
            if (node.Attributes?["name"] != null && node.Attributes["name"].Value == "Everest") {
                to.DocumentElement.RemoveChild(node);
            }
        }

        // Add an Everest tag onto the docs to be added
        XmlAttribute attrib = from.CreateAttribute("name");
        attrib.Value = "Everest";
        from.DocumentElement.LastChild.Attributes.Append(attrib);

        to.DocumentElement.AppendChild(to.ImportNode(from.DocumentElement.LastChild, true));
        to.Save(xmlTo);
    }

    private static void CreateOrigDoc(XmlNode node, ref XmlNode origDoc) {
        string cref = node.Attributes["cref"]?.Value;
        if (cref == null) {
            cref = node.ParentNode.Attributes["name"].Value.Replace("orig_", "");
        }

        origDoc.InnerXml = "Vanilla Method. Use <see cref=\"" + cref + "\"/> instead.";
    }
}