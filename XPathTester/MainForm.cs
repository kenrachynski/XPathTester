using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

namespace XPathTester
{
    public partial class MainForm : Form
    {
        private XmlDocument LoadedXml { get; } = new XmlDocument();

        private Hashtable Namespaces { get; set; }

        private XPathDocument XPathDocument { get; set; }

        public MainForm()
        {
            InitializeComponent();
        }

        private void listboxNamespace_SelectedIndexChanged(object sender, EventArgs e)
        {
            string[] s = listboxNamespace.Items[listboxNamespace.SelectedIndex].ToString().Split('\t');

            textboxXPath.Text = $@"//{s[0]}:*";
        }

        #region GUI Handlers

        private void fileOpenButton_Click(object sender, EventArgs e)
        {
            FileInfo file = null;

            if (textboxFilename.Text.Trim().Length > 0)
            {
                file = new FileInfo(textboxFilename.Text);
                if (file.Directory != null) openFileDialog1.InitialDirectory = file.Directory.FullName;
            }

            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            file = new FileInfo(openFileDialog1.FileName);

            LoadGivenFile(file);
            textboxFilename.Text = file.FullName;
        }

        private void inputXmlTextbox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (textboxInputXml.Focused)
                {
                    LoadedXml.LoadXml(textboxInputXml.Text);

                    LoadNamespaces();
                }

                FileInfo fi = new FileInfo(Application.ExecutablePath);

                LoadedXml.Save(fi.Directory + @"\input.xml");

                XPathDocument = new XPathDocument(fi.Directory + @"\input.xml");
                textboxResultXml.Text = ParseXPath(textboxXPath.Text);
            }
            catch (Exception ex)
            {
                textboxResultXml.Text = $"Message=[{ex.Message}]";
            }
        }

        private void XPathTextbox_TextChanged(object sender, EventArgs e)
        {
            textboxResultXml.Text = ParseXPath(textboxXPath.Text);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Namespaces = new Hashtable();
        }

        private void filenameTextbox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != '\r') return;
            FileInfo file = new FileInfo(textboxFilename.Text);

            LoadGivenFile(file);
            e.Handled = true;
        }

        private void linkLabelReformat_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                textboxInputXml.Text = Format();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void linkLabelSave_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (textboxInputXml.Text.Trim().Length <= 0) return;
            if (textboxFilename.Text.Trim().Length <= 0) return;
            FileInfo file = new FileInfo(textboxFilename.Text);
            bool write = true;

            if (file.Exists)
                if (
                    MessageBox.Show(
                        $"File [{file.Name}] already exists, do you want to overwrite?",
                        @"Overwrite?", MessageBoxButtons.YesNo) == DialogResult.No)
                    write = false;

            if (write)
            {
                LoadedXml.Save(file.FullName);
            }
        }

        private void treeViewNodeHierarchy_AfterSelect(object sender, TreeViewEventArgs e)
        {
            XmlNode xmlNode = (XmlNode) e.Node.Tag;

            if (e.Node.Nodes.Count == 0 && xmlNode.HasChildNodes)
                LoadChildren(e.Node, xmlNode);

            XmlNode thisNode = xmlNode.CloneNode(false);
            textBoxSelectedNode.Text = thisNode.OuterXml;

            if (comboBoxTracking.SelectedIndex > 0)
            {
                textboxXPath.Text = GetPath(comboBoxTracking.SelectedIndex, xmlNode);
            }
        }

        private void comboBoxTracking_SelectedIndexChanged(object sender, EventArgs e)
        {
            TreeNode node = treeViewNodeHierarchy.SelectedNode;

            if (node == null) return;
            if (comboBoxTracking.SelectedIndex <= 0) return;
            XmlNode xmlNode = (XmlNode) node.Tag;
            textboxXPath.Text = GetPath(comboBoxTracking.SelectedIndex, xmlNode);
        }

        #endregion

        #region Private Methods

        private string ParseXPath(string xPath)
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                if (XPathDocument != null)
                {
                    XPathNavigator navigator = XPathDocument.CreateNavigator();
                    XPathExpression expression = navigator.Compile(xPath);

                    XmlNamespaceManager xnm = new XmlNamespaceManager(new NameTable());

                    foreach (DictionaryEntry de in Namespaces)
                    {
                        xnm.AddNamespace((string) de.Key, (string) de.Value);
                    }

                    expression.SetContext(xnm);

                    XPathNodeIterator xni = navigator.Select(expression);

                    sb.AppendFormat("Count: [{0}]", xni.Count);
                    sb.AppendLine();
                    while (xni.MoveNext())
                    {
                        sb.AppendFormat("[{0}] - {1}", xni.CurrentPosition, xni.Current.OuterXml);
                        sb.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                sb.Append(ex.Message);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void LoadNamespaces()
        {
            Namespaces.Clear();

            LoadNamespaces(LoadedXml.DocumentElement);

            listboxNamespace.BeginUpdate();
            listboxNamespace.Items.Clear();

            foreach (DictionaryEntry de in Namespaces)
            {
                listboxNamespace.Items.Add(de.Key + "\t" + de.Value);
            }

            listboxNamespace.EndUpdate();
        }

        private void LoadNamespaces(XmlNode node)
        {
            if (!string.IsNullOrEmpty(node?.NamespaceURI))
            {
                if (!string.IsNullOrEmpty(node.Prefix))
                {
                    if (node.Prefix == "xmlns")
                    {
                        if (!string.IsNullOrEmpty(node.LocalName))
                        {
                            if (!Namespaces.ContainsKey(node.LocalName))
                                Namespaces.Add(node.LocalName, node.Value);
                        }
                    }
                    else
                    {
                        if (!Namespaces.ContainsKey(node.Prefix))
                            Namespaces.Add(node.Prefix, node.NamespaceURI);
                    }
                }
                else if (!Namespaces.ContainsKey("ns"))
                    Namespaces.Add("ns", node.NamespaceURI);
            }
            if (node?.Attributes != null)
            {
                foreach (XmlAttribute attrib in node.Attributes)
                {
                    LoadNamespaces(attrib);
                }
            }

            if (node?.ChildNodes == null) return;
            foreach (XmlNode child in node.ChildNodes)
            {
                LoadNamespaces(child);
            }
        }

        private void LoadGivenFile(FileSystemInfo file)
        {
            string content = "";

            try
            {
                //load the TreeView
                treeViewNodeHierarchy.BeginUpdate();
                treeViewNodeHierarchy.Nodes.Clear();

                if (file.Exists)
                {
                    LoadedXml.Load(file.FullName);
                    LoadNamespaces();

                    content = Format();

                    if (LoadedXml.DocumentElement != null)
                    {
                        TreeNode node = new TreeNode
                        {
                            Text = LoadedXml.DocumentElement.Name,
                            Tag = LoadedXml.DocumentElement
                        };
                        treeViewNodeHierarchy.Nodes.Add(node);
                    }
                }
                else
                {
                    content = "File doesn't exist";
                }
            }
            catch (Exception ex)
            {
                content = ex.Message;
            }
            finally
            {
                treeViewNodeHierarchy.EndUpdate();
            }

            textboxInputXml.Text = content;
        }

        private string Format()
        {
            string content = "";

            using (StringWriter stringWriter = new StringWriter())
            {
                using (XmlNodeReader xmlNodeReader = new XmlNodeReader(LoadedXml))
                {
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "\t",
                        NewLineChars = "\r\n",
                        NewLineOnAttributes = true
                    };
                    using (XmlWriter tw = XmlWriter.Create(stringWriter, settings))
                    {
                        //tw.Formatting = Formatting.Indented;
                        //tw.Indentation = 1;
                        //tw.IndentChar = '\t';

                        tw.WriteNode(xmlNodeReader, false);

                        tw.Flush();

                        content = stringWriter.ToString();
                    }
                }
            }

            return content;
        }

        private static void LoadChildren(TreeNode parentNode, XmlNode parentXml)
        {
            foreach (TreeNode childNode in from XmlNode childXml in parentXml.ChildNodes select new TreeNode
            {
                Text = childXml.Name,
                Tag = childXml
            })
            {
                parentNode.Nodes.Add(childNode);
            }
        }

        private static string GetPath(int trackingType, XmlNode node)
        {
            XmlNode runner = node;

            Stack<string> stack = new Stack<string>();

            switch (trackingType)
            {
                case 1: //Node Name only
                    do
                    {
                        runner = runner.ParentNode;
                    } while (runner != null && runner.Name != "#document");

                    break;
                case 2: //Positional
                    do
                    {
                        stack.Push(runner.Name);
                        runner = runner.ParentNode;
                    } while (runner != null && runner.Name != "#document");
                    break;
                case 3: //Attribute differentiator
                    do
                    {
                        stack.Push(runner.Name);
                        runner = runner.ParentNode;
                    } while (runner != null && runner.Name != "#document");
                    break;
            }

            return string.Join("/", stack.ToArray());
        }

        #endregion
    }
}