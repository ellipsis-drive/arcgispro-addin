using Ellipsis.Api;
using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Input;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using TestAddIn;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Core.Data.UtilityNetwork.Trace;

namespace Ellipsis.Drive
{
    //These are basically describing how event handler functions (or CB's) have to look.
    public delegate void LayerEvent(JObject block, JObject layer);
    public delegate void TimestampEvent(JObject block, JObject timeStamp, JObject visualization, string protocol);

    class DriveView
    {
        private System.Windows.Controls.TreeView view;
        private System.Windows.Forms.Form parent;
        private Connect connect;
        private TextBox searchInput;

        class DriveViewItem : TreeViewItem
        {
            public DriveViewItem(int _level) { level = _level; }

            public static readonly RoutedEvent expandingEvent =
                EventManager.RegisterRoutedEvent("expanding",
                RoutingStrategy.Bubble, typeof(RoutedEventHandler),
                typeof(DriveViewItem));

            public event RoutedEventHandler expanding
            {
                add { AddHandler(expandingEvent, value); }
                remove { RemoveHandler(expandingEvent, value); }
            }

            protected override void OnExpanded(RoutedEventArgs e)
            {
                onExpanding(new RoutedEventArgs(expandingEvent, this));
                base.OnExpanded(e);
            }

            protected virtual void onExpanding(RoutedEventArgs e) { RaiseEvent(e); }

            public static readonly RoutedEvent doubleClickEvent =
                EventManager.RegisterRoutedEvent("doubleClick",
                RoutingStrategy.Bubble, typeof(RoutedEventHandler),
                typeof(DriveViewItem));

            public event RoutedEventHandler doubleClick
            {
                add { AddHandler(doubleClickEvent, value); }
                remove { RemoveHandler(doubleClickEvent, value); }
            }

            protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
            {
                onDoubleClick(new RoutedEventArgs(doubleClickEvent, this));
                base.OnMouseDoubleClick(e);
            }

            protected virtual void onDoubleClick(RoutedEventArgs e) { RaiseEvent(e); }

            public void SetLevel(int _level) { level = _level; }
            public int GetLevel() { return level; }
            protected int level;
            public string Title;
            public JObject Info;
        }

        //Event fires when user doubleclicks on a (vector) layer.
        public event LayerEvent onLayerClick;
        //Event fires when user doubleclicks on a (raster) timestamp.
        public event TimestampEvent onTimestampClick;
        //Event fires when user selects a (vector) layer.
        public event LayerEvent onLayerSelect;
        //Event fires when user selects a (raster) timestamp.
        public event TimestampEvent onTimestampSelect;

        //Load ellipsis drive folders in view. Images are used to show folder and corresponding block icons.
        //DriveView will handle searchInput changes, refreshButton clicks and openInBrowser clicks.
        public DriveView(System.Windows.Controls.TreeView view, Connect connect, /*ImageList images,*/ TextBox searchInput, Button refreshButton, Button openInBrowser, System.Windows.Forms.Form parent)
        {
            this.view = view;
            this.connect = connect;
            this.parent = parent;
            /*
            if (images != null && images.Images != null && images.Images.Count >= 3)
            {
                view.ImageList = images;
            }
            */
            if (searchInput != null)
            {
                searchInput.TextChanged += handleSearch;
                searchInput.GotFocus += searchbox_GotFocus;
                searchInput.LostFocus += searchBox_LostFocus;
                this.searchInput = searchInput;
            }

            if (refreshButton != null)
            {
                refreshButton.Click += handleRefreshClick;
            }

            if (openInBrowser != null)
            {
                openInBrowser.Click += (sender, e) => handleOpenInBrowserClick(sender, e);
            }
            /*
            view.Indent = 12;
            view.BeforeExpand += handleNodeExpand;
            */
            view.AddHandler(DriveViewItem.expandingEvent, new RoutedEventHandler(handleNodeExpand));
            view.AddHandler(DriveViewItem.doubleClickEvent, new RoutedEventHandler(handleDoubleClick));

            //Empty event handlers because we cannot pass empty event handlers into runInfoCallback.
            onLayerClick += (a, b) => { };
            onTimestampClick += (a, b, c, d) => { };
            onLayerSelect += (a, b) => { };
            onTimestampSelect += (a, b, c, d) => { };

            //Map node click event handlers to run info callbacks.
            //view.NodeMouseDoubleClick += (sender, e) => runInfoCallbackForNode(e.Node, onLayerClick.Invoke, onTimestampClick.Invoke);
            //view.AfterSelect += (sender, e) => runInfoCallbackForNode(e.Node, onLayerSelect.Invoke, onTimestampSelect.Invoke);
            view.SelectedItemChanged += (sender, e) => handleSelect(null, null, openInBrowser);
            //Set root nodes.
            resetNodes();
        }

        private void SearchInput_GotFocus(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void handleDoubleClick(object sender, RoutedEventArgs e)
        {
            DriveViewItem vItem = e.Source as DriveViewItem;
            runInfoCallbackForNode(vItem, onLayerClick.Invoke, onTimestampClick.Invoke);
        }

        private void handleSelect(object sender, RoutedEventArgs e, Button openInBrowser)
        {
            openInBrowser.IsEnabled = true;
        }

        //Open current selected node in browser. Return process of current browser.
        private System.Diagnostics.Process handleOpenInBrowserClick(object sender, EventArgs e)
        {
            DriveViewItem vItem = view.SelectedItem as DriveViewItem;
            /* Maybe title, tag or header instead of name */
            if (vItem == null || vItem.Name == "loading") return null;

            string baseUrl = "https://app.ellipsis-drive.com";
            if (vItem.GetLevel() == 0)
            {
                return System.Diagnostics.Process.Start(new ProcessStartInfo($"{baseUrl}/drive/{vItem.Name}") { UseShellExecute = true });
            }

            if (vItem.Tag != null)
            {
                JObject selectedInfo = vItem.Tag as JObject;
                if (selectedInfo.Value<string>("type") == "folder")
                {
                    var path = selectedInfo.Value<JObject>("path");
                    string root = path.Value<string>("root");
                    string pathId = path.Value<JArray>("path")[0].Value<string>("id");

                    return System.Diagnostics.Process.Start(new ProcessStartInfo($"{baseUrl}/drive/{root}?pathId={pathId}") { UseShellExecute = true });

                }
                if (selectedInfo.Value<string>("type") == "map" || selectedInfo.Value<string>("type") == "shape")
                {
                    return System.Diagnostics.Process.Start(new ProcessStartInfo($"{baseUrl}/view?mapId={selectedInfo.Value<string>("id")}") { UseShellExecute = true });
                }

                System.Diagnostics.Process started = null;
                runInfoCallbackForNode(vItem, (block, layer) =>
                {
                    started = System.Diagnostics.Process.Start(new ProcessStartInfo($"{baseUrl}/view?mapId={block.Value<string>("id")}") { UseShellExecute = true });
                }, (block, timestamp, visualization, protocol) =>
                {
                    started = System.Diagnostics.Process.Start(new ProcessStartInfo($"{baseUrl}/view?mapId={block.Value<string>("id")}") { UseShellExecute = true });
                }, true);

                return started;
            }

            return null;
        }
        //Refreshes view.
        private void handleRefreshClick(object sender, EventArgs e)
        {
            if (searchInput.Text.Trim() == "")
            {
                resetNodes();
            }
            else
            {
                DriveViewItem vItem = (DriveViewItem)view.Items[0];
                if (view.Items.Count != 1 || vItem.Name != "loading")
                {
                    _ = searchDrive(searchInput.Text);
                }
            }
        }
        //Resets nodes to the three root nodes.
        private void deleteRecursive(ItemCollection items)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                TreeViewItem vItem = (TreeViewItem)items[i];
                //DriveViewItem vItem = (DriveViewItem)items[i];
                deleteRecursive(vItem.Items);
                items.RemoveAt(i);
            }
        }

        private void addNode(DriveViewItem parent, string header)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                DriveViewItem newItem = new DriveViewItem(parent.GetLevel() + 1);
                newItem.Header = header;
                parent.Items.Add(newItem);
            });
        }

        private void initRoot(ItemCollection parent, string header, string name)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                DriveViewItem newItem = new DriveViewItem(0);
                newItem.Header = header;
                newItem.Title = name;
                parent.Add(newItem);
            });
        }

        public void resetNodes()
        {
            deleteRecursive(view.Items);
            initRoot(view.Items, "My Drive", "myDrive");
            initRoot(view.Items, "Shared with me", "sharedWithMe");
            initRoot(view.Items, "Favorites", "favorites");

            foreach (DriveViewItem rootFolder in view.Items)
            {
                addNode(rootFolder, "Loading...");
            }
        }
        

        //Handle searchInput change.
        private void handleSearch(object sender, EventArgs e)
        {
            if (searchInput.Text.Trim() == "")
            {
                resetNodes();
                return;
            }
            if (searchInput.Text.Trim() != "Search..." )
                _ = searchDrive(searchInput.Text);
        }

        //Looks at context of node to find all relevant information. Runs layerCb if it's
        //a shape and timestampCb if it's a map. See delegates for parameters of these cb's.
        private void runInfoCallbackForNode(DriveViewItem node, LayerEvent layerCb, TimestampEvent timestampCb, bool from_browser = false)
        {
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
            JObject nodeTag = node.Tag as JObject;
            string baseUrl = "https://api.ellipsis-drive.com/v3/ogc";
            if (nodeTag == null) return;

            if (nodeTag.Value<string>("method") != null)
            {
                //node tag is visualization
                DriveViewItem parent = node.Parent as DriveViewItem;
                DriveViewItem parent_parent = parent.Parent as DriveViewItem;
                DriveViewItem parent_parent_parent = parent_parent.Parent as DriveViewItem;
                string protocol = parent.Title;
                JObject timestamp = parent_parent.Tag as JObject;
                JObject block = parent_parent_parent.Tag as JObject;

                timestampCb(block, timestamp, nodeTag, protocol);
                if (protocol == "WMS")
                {
                    Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), nodeTag.Value<string>("id"), null);
                    _ = layer.AddWMSAsync();
                }
                else if (protocol == "WCS")
                {
                    Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), nodeTag.Value<string>("id"), null);
                    _ = layer.AddWCS();
                }
                else if (protocol == "WMTS")
                {
                    Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), nodeTag.Value<string>("id"), null);
                    _ = layer.AddWMTS();
                }

            }

            else if (nodeTag.Value<string>("dateFrom") != null && ((node.Header as string) == "WCS" || (node.Header as string) == "WMTS"))
            {
                //node tag is timestamp
                //This is a protocol with no visualization or the WMTS protocol that doesn't render visualizations
                if (node.Items.Count == 0)
                {
                    DriveViewItem parent = node.Parent as DriveViewItem;
                    string protocol = node.Header as string;
                    JObject timestamp = nodeTag;
                        
                    DriveViewItem parent_parent = parent.Parent as DriveViewItem;
                    JObject block = parent_parent.Tag as JObject;
                    JObject maplayer = block["mapLayers"][0] as JObject;
                    timestampCb(block, timestamp, null, protocol);
                    if (protocol == "WCS")
                    {
                        Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), nodeTag.Value<string>("id"), null);
                        _ = layer.AddWCS();
                    }
                    else if (protocol == "WMS")
                    {
                        Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), nodeTag.Value<string>("id"), null);
                        _ = layer.AddWMSAsync();
                    }
                    else if (protocol == "WMTS")
                    {
                        Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), maplayer.Value<string>("id"), null);
                        _ = layer.AddWMTS();
                    }
                }
            }

            else if (nodeTag.Value<JObject>("extent") != null)
            {
                string protocol = node.Header as string;
                DriveViewItem parent = node.Parent as DriveViewItem;
                JObject block = parent.Tag as JObject;
                if (protocol == "WMTS")
                {
                    JObject timestamp = nodeTag;
                    JObject rasterInfo = parent.Info.Value<JObject>("raster");
                    JObject maplayer = rasterInfo["styles"][0] as JObject;
                    timestampCb(block, timestamp, null, protocol);
                    Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), protocol, timestamp.Value<string>("id"), maplayer.Value<string>("id"), null);
                    _ = layer.AddWMTS();
                }
                else 
                {
                    layerCb(block, nodeTag);
                    //node tag is geometryLayer
                    if (block.Value<string>("type") == "vector" && from_browser == false)
                    {
                        string pathId = block.Value<string>("id");
                        var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://api.ellipsis-drive.com/v3/account/security/accessToken");

                        object[] a = new object[1];
                        a[0] = new { pathId = pathId, access = new { accessLevel = 100 } };

                        var body = JsonConvert.SerializeObject(new
                        {
                            description = "ArcGIS Pro WFS Token",
                            accessList = a
                        });

                        string loginToken = this.connect.GetLoginToken();

                        httpWebRequest.ContentType = "application/json";
                        httpWebRequest.Method = "POST";
                        httpWebRequest.Headers.Add("Authorization", "Bearer " + loginToken);
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                        using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                        {
                            streamWriter.Write(body);
                            streamWriter.Flush();
                        }


                        var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                        StreamReader reader = new StreamReader(httpResponse.GetResponseStream());
                        string responseFromServer = reader.ReadToEnd();
                        JObject data = JObject.Parse(responseFromServer);

                        string accessToken = data.Value<string>("token");
                        string wfsUrl = $"https://api.ellipsis-drive.com/v3/ogc/wfs/{pathId}?token={accessToken}";

                        /*DialogForm dialogForm = new DialogForm();
                        dialogForm.SetUrl(wfsUrl);
                        dialogForm.ShowDialog(this.parent);
*/
                        NoticeWindow w = new NoticeWindow();
                        w.SetUrl(wfsUrl);
                        w.ShowDialog();

                      /*  string message = "Add the following URL as WFS: " + ;
                        string title = "Notice";
                        System.Windows.Forms.MessageBox.Show(message, title, System.Windows.Forms.MessageBoxButtons.OK);*/
                        /*     Layers layer = new Layers(baseUrl, block.Value<string>("id"), this.connect.GetLoginToken(), "wfs", null, null, null);
                             _ = layer.AddWFS();*/
                    }
                } 
            }
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
        }

        //Handle expansion of node. Will start loading if needed.
        private void handleNodeExpand(object sender, RoutedEventArgs e)
        {
            //Every folder contains loading node by default, so upon opening, convert this to contents.
            DriveViewItem vItem = e.Source as DriveViewItem;
            #pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
            if (vItem.Items == null || vItem.Items.Count != 1 || (vItem.Items[0] as TreeViewItem).Header != "Loading...")
            {
                return;
            }
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
            var loadingMarker = vItem.Items[0] as TreeViewItem;
            if (!loadingMarker.Header.Equals("Loading...")) return;
            System.Windows.Application.Current.Dispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
            {
                _ = loadFolder(vItem);
                view.Items.Refresh();
            });
        }
        

        
        //Constructs a treenode.
        private DriveViewItem getNode(string name, int level, string id = null, object tag = null)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                return new DriveViewItem(level)
                {
                    Header = name,
                    Title = id,
                    Tag = tag,
                    //ImageIndex = 3,
                    //SelectedImageIndex = 3,
                    //StateImageIndex = 3
                };
            });
        }

        //Gets folder node from info. Checks for all render conditions.
        private DriveViewItem getFolderNode(JObject info, int level)
        {
            DriveViewItem folder = getNode(info.Value<string>("name"), level, info.Value<string>("id"));
            folder.Info = info;
            
            return Application.Current.Dispatcher.Invoke(() =>
            {
                folder.Tag = info;
                //folder.ImageIndex = 0;
                //folder.SelectedImageIndex = 0;
                //folder.StateImageIndex = 0;

                if (info.Value<bool>("trashed"))
                {
                    folder.Header += " (folder deleted)";
                    return folder;
                }
                if (info.Value<bool>("disabled"))
                {
                    folder.Header += " (folder disabled)";
                    return folder;
                }
                if (info.Value<JObject>("yourAccess").Value<int>("accessLevel") <= 0)
                {
                    folder.Header += " (access level too low)";
                    return folder;
                }
                addNode(folder, "Loading...");
                return folder;
            });
        }

        //Converts layers into a list of corresponding treenodes.
        private List<DriveViewItem> getLayerNodes(JArray layers, int level)
        {
            //Regex to keep naming convention in web app:
            List<DriveViewItem> nodes = new List<DriveViewItem>();
            var children = layers.Children<JObject>();

            foreach (var child in children)
            {
                //TODO parse {availability: {blocked: false}} ??
                if (child.Value<bool>("trashed"))
                    continue;
                JObject dateObject = child.Value<JObject>("date");

                string timestampName = dateObject.Value<string>("from") + " - " + dateObject.Value<string>("to");

                DriveViewItem parsedChild = getNode(timestampName, level+1, child.Value<string>("id"), timestampName);
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    parsedChild.Tag = child;
                    nodes.Add(parsedChild);
                });
            }
            return nodes;
        }
        

        //Converts visualizations into a list of corresponding treenodes.
        private List<DriveViewItem> getVisualizationNodes(JArray visualizations, int level)
        {
            string node_name;
            //Regex to keep naming convention in web app:
            Regex rx = new Regex(@"\d{2,2}/\d{2,2}/\d{4,4} \d{2,2}:\d{2,2}:\d{2,2}");
            List <DriveViewItem> visualizationNodes = new List<DriveViewItem>();
            foreach (var child in visualizations.Children<JObject>())
            {
                node_name = child.Value<string>("name");
                Debug.WriteLine(node_name, "node_name: ");
                if (rx.IsMatch(node_name))
                {
                    node_name += ' ' + node_name;
                }
                visualizationNodes.Add(getNode(node_name, level, child.Value<string>("id"), child));
            }
            Debug.WriteLine("Visualization Nodes:");
            Debug.WriteLine(visualizationNodes);
            return visualizationNodes;
        }

        //Get treenodes to represent the protocols.
        private DriveViewItem[] getProtocolNode(JObject timestamp, int level, JObject pathInfo)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                DriveViewItem wmsNode = getNode("WMS", level + 1, "WMS", timestamp);
                DriveViewItem wmtsNode = getNode("WMTS", level + 1, "WMTS", timestamp);

                wmsNode.Info = pathInfo;
                wmtsNode.Info = pathInfo;

                return new DriveViewItem[]
                {
                    wmsNode, wmtsNode
                };
            });    
        }

        //Convert timestamps into a list of corresponding treenodes.
        private List<DriveViewItem> getTimestampNodes(JArray timestamps, int level, JObject info)
        {
            List<DriveViewItem> nodes = new List<DriveViewItem>();
            var children = timestamps.Children<JObject>();
            foreach (var child in children)
            {
                JObject dateObject = child.Value<JObject>("date");
                string timestampName = dateObject.Value<string>("from") + " - " + child.Value<string>("to");

                DriveViewItem parsedChild = getNode(timestampName, level + 1, child.Value<string>("id"));
                parsedChild.Info = info;

                //TODO parse {availability: {blocked: false}} ??
                if (child.Value<string>("status") == "deleted")
                {
                    Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        parsedChild.Header += " (Deleted)";
                        //nodes.Add(parsedChild);
                    });
                    continue;
                }
                
                if (child.Value<string>("status") != "active")
                {
                    Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        parsedChild.Header += " (Unavailable)";
                        nodes.Add(parsedChild);
                    });
                    continue;
                }

                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    parsedChild.Tag = child;
                    nodes.Add(parsedChild);
                });
            }
            return nodes;
        }
        
        //Gets block node from info. Checks for all render conditions.
        private DriveViewItem getBlockNode(JObject info, int level)
        {
            //Check for deleted, no time stamps, no layers, disabled, low access level
            DriveViewItem block = getNode(info.Value<string>("name"), level, info.Value<string>("id"));
            block.Info = info;

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                block.Tag = info;
            });

            if (info.Value<bool>("deleted"))
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    block.Header += " (block deleted)";
                    return block;
                });
            }
            if (info.Value<bool>("disabled"))
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    block.Header += " (block disabled)";
                    return block;
                });
            }

            if (info.Value<string>("type") == "vector")
            {
                JObject vectorInfo = info.Value<JObject>("vector");

                JArray layers = vectorInfo.Value<JArray>("timestamps");
                //block.ImageIndex = 2;
                //block.SelectedImageIndex = 2;
                //block.StateImageIndex = 2;
                if (layers.Count == 0)
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        block.Header += " (vector has no layers)";
                        return block;
                    });
                }

                var layerNodes = getLayerNodes(layers, level);
                if (layerNodes.Count == 0)
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        block.Header += " (vector has no layers)";
                        return block;
                    });
                }
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    foreach (DriveViewItem vItem in layerNodes)
                    {
                        block.Items.Add(vItem);
                    }
                });
            }
            else if (info.Value<string>("type") == "raster")
            {
                JObject rasterInfo = info.Value<JObject>("raster");

                JArray timestamps = rasterInfo.Value<JArray>("timestamps");
                //block.ImageIndex = 1;
                //block.SelectedImageIndex = 1;
                //block.StateImageIndex = 1;
                if (timestamps.Count == 0)
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        block.Header += " (raster has no timestamps)";
                        return block;
                    });
                }

                var timestampNodes = getTimestampNodes(timestamps, level, info);

                if (timestampNodes.Count == 0)
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        block.Header += " (raster has no active timestamps)";
                        return block;
                    });
                }

                JArray visualizations = rasterInfo.Value<JArray>("styles");
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    timestampNodes.ForEach(t =>
                    {
                        string t_header = t.Header as string;
                        if (!t_header.Contains("(Unavailable)") && !t_header.Contains("(Deleted)"))
                        {
                            DriveViewItem[] protocols = getProtocolNode((JObject)t.Tag, level, info);
                            foreach (DriveViewItem protocol in getVisualizationNodes(visualizations, level).ToArray())
                            {
                                protocols[0].Items.Add(protocol);
                            }
                            foreach (DriveViewItem protocol in getVisualizationNodes(visualizations, level).ToArray())
                            {
                                protocols[1].Items.Add(protocol);
                            }
                            foreach (DriveViewItem protocol in protocols)
                            {
                                t.Items.Add(protocol);
                            }
                        }
                    });

                    foreach (DriveViewItem item in timestampNodes.ToArray())
                    {
                        block.Items.Add(item);
                    }
                    
                });
            }

            return block;
        }

        //Fetch all folders inside parent folder.
        private List<DriveViewItem> fetchFolders(DriveViewItem parent)
        {
            List<DriveViewItem> buffer = new List<DriveViewItem>();

            string nextFolderPageStart = null;
            do
            {
                JObject nestedFolders = null;
                System.Windows.Application.Current.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                {
                    nestedFolders = connect.GetPath(parent.Title, true, nextFolderPageStart, parent.GetLevel() == 0);
                });
                foreach (JObject nestedFolder in nestedFolders["result"]) // <-- Note that here we used JObject instead of usual JProperty
                {
                    bool trashed = nestedFolder.Value<bool>("trashed");

                    if (trashed)
                    {
                        continue;
                    }

                    JObject owner = nestedFolder.Value<JObject>("user");
                    bool disabled = owner.Value<bool>("disabled");

                    if (disabled)
                    {
                        continue;
                    }


                    string type = nestedFolder.Value<string>("type");

                    if (type == "folder")
                    {
                        buffer.Add(getFolderNode(nestedFolder, parent.GetLevel() + 1));
                    }
                    else if (type == "raster" || type == "vector")
                    {
                        buffer.Add(getBlockNode(nestedFolder, parent.GetLevel() + 1));
                    }

                }
                nextFolderPageStart = nestedFolders.Value<string>("nextPageStart");
            } while (nextFolderPageStart != null);

            return buffer;
        }

        private void searchbox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (this.searchInput.Text == "Search...")
                this.searchInput.Text = "";
        }

        private void searchBox_LostFocus(object sender, EventArgs e)
        {
            if (this.searchInput.Text == "")
                this.searchInput.Text = "Search...";
        }

        //Fetch all blocks and folders inside folder asynchrounously
        private async Task loadFolder(DriveViewItem folder)
        {
            var results = await Task.WhenAll(
                Task.Run(() => fetchFolders(folder))
            );

            folder.Items.RemoveAt(0);

            if (results[0] != null)
            {
                foreach (DriveViewItem vItem in results[0].ToArray())
                {
                    folder.Items.Add(vItem);
                }
            }
        }

        private List<DriveViewItem> searchPaths(string name)
        {
            List<DriveViewItem> buffer = new List<DriveViewItem>();

            string nextFolderPageStart = null;
            do
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (name != searchInput.Text)
                        buffer = null;
                });

                JObject paths = connect.SearchByName(name, nextFolderPageStart);
                foreach (JObject path in paths["result"])
                {
                    if (buffer != null)
                    {
                        string type = path.Value<string>("type");
                        if (type == "folder")
                        {
                            buffer.Add(getFolderNode(path, 0));
                        }
                        else if (type == "raster" || type == "vector")
                        {
                            buffer.Add(getBlockNode(path, 0));
                        }
                    }
                    else
                        return buffer;
                }
                nextFolderPageStart = paths.Value<string>("nextPageStart");
            } while (nextFolderPageStart != null);

            return buffer;
        }

        //Searches drive asynchronously until name is not equal to searchInput text.
        private async Task searchDrive(string name)
        {
            view.Items.Clear();
            initRoot(view.Items, "Loading...", "loading");
            var task1 = await Task.Run(() => searchPaths(name));

            if (name != searchInput.Text || task1.Any((x) => x == null)) return;

            view.Items.Clear();
            initRoot(view.Items, "Result", "result");
            DriveViewItem subView = view.Items[0] as DriveViewItem;
            if (task1 != null)
            {
                foreach (DriveViewItem vItem in task1.ToArray())
                {
                    vItem.SetLevel(1);
                    subView.Items.Add(vItem);
                }
            }
            DriveViewItem subSubView = subView.Items[0] as DriveViewItem;
            Debug.WriteLine("Sub level: ");
            Debug.WriteLine(subSubView.GetLevel().ToString());
        }
    }
}
