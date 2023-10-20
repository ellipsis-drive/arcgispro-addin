using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using ArcGIS.Desktop.Internal.GeoProcessing.Controls;
using ArcGIS.Core.Data.UtilityNetwork.Trace;

namespace Ellipsis.Api
{
    class Layers
    {
        public Layers() { }

        public Layers(string _URL, string _map_id, string _login_token, string _protocol, string _timestamp_id, string _layer_id, string _BBOX)
        {
            URL = _URL;
            map_id = _map_id;
            login_token = _login_token;
            timestamp_id = _timestamp_id;
            layer_id = _layer_id;
            protocol = _protocol.ToLower();
            BBOX = _BBOX;
            url = string.Format("{0}/{1}/{2}/{3}", URL, protocol, map_id, login_token);
            if (protocol == "wmts" || protocol == "wms")
                ids = string.Format("{0}_{1}", timestamp_id, layer_id);
            else if (protocol == "wfs")
            {
                ids = string.Format("layerId_{0}", layer_id);
                url = string.Format("{0}/{1}/{2}", URL, protocol, map_id);
            }

                //activeView.Activate();
            }

        public async Task AddWCS()
        {
            // Create a connection to the WMS server
            var serverConnection = new CIMInternetServerConnection { URL = url };
            Debug.WriteLine("url");
            Debug.WriteLine(url);
            var connection = new CIMWCSServiceConnection { ServerConnection = serverConnection, CoverageName = ids };

            // Add a new layer to the map
            //await QueuedTask.Run(() => LayerFactory.Instance.CreateLayer(connection, MapView.Active.Map));
        }

        public async Task AddWMSAsync()
        {
            var serverConnection = new CIMInternetServerConnection { URL = url };
            var connection = new CIMWMSServiceConnection { ServerConnection = serverConnection, LayerName = ids };

            // Add a new layer to the map
            var layerParams = new LayerCreationParams(connection);
            await QueuedTask.Run(() =>
            {
                var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, MapView.Active.Map);
            });
        }
        public async Task AddWMTS()
        {
            var serverConnection = new CIMInternetServerConnection { URL = url };
            var connection = new CIMWMTSServiceConnection { ServerConnection = serverConnection, LayerName = ids };

            // Add a new layer to the map
            var layerParams = new LayerCreationParams(connection);
            await QueuedTask.Run(() =>
            {
                var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, MapView.Active.Map);
            });
        }

        public async Task AddWFS()
        {
            /*        CIMStandardDataConnection cIMStandardDataConnection = new CIMStandardDataConnection()
                    {
                        WorkspaceConnectionString = @"SWAPXY=TRUE;SWAPXYFILTER=FALSE;URL=http://sampleserver6.arcgisonline.com/arcgis/services/SampleWorldCities/MapServer/WFSServer;VERSION=2.0.0",
                        WorkspaceFactory = WorkspaceFactory.WFS,
                        Dataset = "Continent",
                        DatasetType = esriDatasetType.esriDTFeatureClass
                    };

                    // Add a new layer to the map
                    var layerPamsDC = new LayerCreationParams(cIMStandardDataConnection);*/

            /*      var serverConnection = new CIMInternetServerConnection { URL = url };
                  var connection = new CIMWFSServiceConnection { ServerConnection = serverConnection, LayerName = ids };

                  // Add a new layer to the map
                  var layerParams = new LayerCreationParams(connection);

                  await QueuedTask.Run(() =>
                  {
                      Layer layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, MapView.Active.Map);
                  });*/

            CIMStandardDataConnection cIMStandardDataConnection = new CIMStandardDataConnection()
            {
                WorkspaceConnectionString = @"SWAPXY=TRUE;SWAPXYFILTER=FALSE;URL=http://sampleserver6.arcgisonline.com/arcgis/services/SampleWorldCities/MapServer/WFSServer;VERSION=2.0.0",
                WorkspaceFactory = WorkspaceFactory.WFS,
                Dataset = "Continent",
                DatasetType = esriDatasetType.esriDTFeatureClass
            };

            // Add a new layer to the map
            var layerPamsDC = new LayerCreationParams(cIMStandardDataConnection);
            await QueuedTask.Run(() =>
            {
                Layer layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerPamsDC, MapView.Active.Map);
            });
        }


        private string URL;
        private string map_id;
        private string login_token;
        private string url;
        private string protocol;
        private string timestamp_id;
        private string layer_id;
        private string ids;
        private string BBOX;
    }
}