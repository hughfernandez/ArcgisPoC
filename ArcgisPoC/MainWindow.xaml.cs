using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace ArcgisPoC
{

    public partial class MainWindow : Window
    {
        // Create overlay to where graphics are shown
        GraphicsOverlay overlay = new GraphicsOverlay();
        
        // Dictionary that associates names with basemaps
        private readonly Dictionary<string, Basemap> basemapOptions = new Dictionary<string, Basemap>()
        {
            {"Streets (Raster)", Basemap.CreateStreets()},
            {"Streets (Vector)", Basemap.CreateStreetsVector()},
            {"Streets With Relief (Vector)", Basemap.CreateStreetsWithReliefVector()},
            {"Streets - Night (Vector)", Basemap.CreateStreetsNightVector()},
            {"Imagery (Raster)", Basemap.CreateImagery()},
            {"Imagery with Labels (Raster)", Basemap.CreateImageryWithLabels()},
            {"Imagery with Labels (Vector)", Basemap.CreateImageryWithLabelsVector()},
            {"Dark Gray Canvas (Vector)", Basemap.CreateDarkGrayCanvasVector()},
            {"Light Gray Canvas (Raster)", Basemap.CreateLightGrayCanvas()},
            {"Light Gray Canvas (Vector)", Basemap.CreateLightGrayCanvasVector()},
            {"Navigation (Vector)", Basemap.CreateNavigationVector()},
            {"OpenStreetMap (Raster)", Basemap.CreateOpenStreetMap()},
            {"TerrainWithLabels (Raster)", Basemap.CreateTerrainWithLabels()},
            {"TerrainWithLabels (Vector)", Basemap.CreateTerrainWithLabelsVector()},
            {"Topographic (Raster)", Basemap.CreateTopographic()},
            {"Topographic (Vector)", Basemap.CreateTopographicVector()},
            {"NationalGeographic", Basemap.CreateNationalGeographic()},
            {"Oceans", Basemap.CreateOceans()}            
        };

        public MainWindow()
        {
            InitializeComponent();

            // Create the UI, setup the control references and execute initialization 
            Initialize();
        }

        private void Initialize()
        {            
            // Assign a new map to the MapView
            MyMapView.Map = new Map(basemapOptions.Values.First());            

            // Set basemap titles as a items source
            BasemapChooser.ItemsSource = basemapOptions.Keys;
            // Show the first basemap in the list
            BasemapChooser.SelectedIndex = 0;
            
            // Add created overlay to the MapView
            MyMapView.GraphicsOverlays.Add(overlay);


            // Fill the combo box with choices for the sketch modes (shapes)
            SketchModeComboBox.ItemsSource = Enum.GetValues(typeof(SketchCreationMode));
            SketchModeComboBox.SelectedIndex = 0;

            // Set the sketch editor configuration to allow vertex editing, resizing, and moving
            var config = MyMapView.SketchEditor.EditConfiguration;
            config.AllowVertexEditing = true;
            config.ResizeMode = SketchResizeMode.Uniform;
            config.AllowMove = true;

            // Set the sketch editor as the page's data context
            DataContext = MyMapView.SketchEditor;

            MyMapView.MouseMove += MyMapView_MouseMove;
            MyMapView.GeoViewTapped += MyMapView_GeoViewTapped;
            MyMapView.DrawStatusChanged += MyMapView_DrawStatusChanged; ;

        }

        #region Graphic and symbol helpers
        private Graphic CreateGraphic(Geometry geometry)
        {
            // Create a graphic to display the specified geometry
            Symbol symbol = null;
            switch (geometry.GeometryType)
            {
                // Symbolize with a fill symbol
                case GeometryType.Envelope:
                case GeometryType.Polygon:
                    {
                        symbol = new SimpleFillSymbol()
                        {
                            Color = Color.Red,
                            Style = SimpleFillSymbolStyle.Solid,
                        };
                        break;
                    }
                // Symbolize with a line symbol
                case GeometryType.Polyline:
                    {
                        symbol = new SimpleLineSymbol()
                        {
                            Color = Color.Red,
                            Style = SimpleLineSymbolStyle.Solid,
                            Width = 5d
                        };
                        break;
                    }
                // Symbolize with a marker symbol
                case GeometryType.Point:
                case GeometryType.Multipoint:
                    {

                        symbol = new SimpleMarkerSymbol()
                        {
                            Color = Color.Red,
                            Style = SimpleMarkerSymbolStyle.Circle,
                            Size = 15d
                        };
                        break;
                    }
            }

            // pass back a new graphic with the appropriate symbol
            return new Graphic(geometry, symbol);
        }

        private async Task<Graphic> GetGraphicAsync()
        {
            // Wait for the user to click a location on the map
            var mapPoint = (MapPoint)await MyMapView.SketchEditor.StartAsync(SketchCreationMode.Point, false);

            // Convert the map point to a screen point
            var screenCoordinate = MyMapView.LocationToScreen(mapPoint);

            // Identify graphics in the graphics overlay using the point
            var results = await MyMapView.IdentifyGraphicsOverlaysAsync(screenCoordinate, 2, false);

            // If results were found, get the first graphic
            Graphic graphic = null;
            IdentifyGraphicsOverlayResult idResult = results.FirstOrDefault();
            if (idResult != null && idResult.Graphics.Count > 0)
            {
                graphic = idResult.Graphics.FirstOrDefault();
            }

            // Return the graphic (or null if none were found)
            return graphic;
        }
        #endregion

        private async void DrawButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Let the user draw on the map view using the chosen sketch mode
                SketchCreationMode creationMode = (SketchCreationMode)SketchModeComboBox.SelectedItem;
                Geometry geometry = await MyMapView.SketchEditor.StartAsync(creationMode, true);

                // Create and add a graphic from the geometry the user drew
                Graphic graphic = CreateGraphic(geometry);
                overlay.Graphics.Add(graphic);

                // Enable/disable the clear and edit buttons according to whether or not graphics exist in the overlay
                ClearButton.IsEnabled = overlay.Graphics.Count > 0;
                EditButton.IsEnabled = overlay.Graphics.Count > 0;
            }
            catch (TaskCanceledException)
            {
                // Ignore ... let the user cancel drawing
            }
            catch (Exception ex)
            {
                // Report exceptions
                MessageBox.Show("Error drawing graphic shape: " + ex.Message);
            }
        }

        private void ClearButtonClick(object sender, RoutedEventArgs e)
        {
            // Remove all graphics from the graphics overlay
            overlay.Graphics.Clear();

            // Disable buttons that require graphics
            ClearButton.IsEnabled = false;
            EditButton.IsEnabled = false;
        }

        private async void EditButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Allow the user to select a graphic
                Graphic editGraphic = await GetGraphicAsync();
                if (editGraphic == null) { return; }

                // Let the user make changes to the graphic's geometry, await the result (updated geometry)
                Geometry newGeometry = await MyMapView.SketchEditor.StartAsync(editGraphic.Geometry);

                // Display the updated geometry in the graphic
                editGraphic.Geometry = newGeometry;
            }
            catch (TaskCanceledException)
            {
                // Ignore ... let the user cancel editing
            }
            catch (Exception ex)
            {
                // Report exceptions
                MessageBox.Show("Error editing shape: " + ex.Message);
            }
        }


        private void MyMapView_DrawStatusChanged(object sender, DrawStatusChangedEventArgs e)
        {
            // Update the load status information
            Dispatcher.Invoke(delegate ()
            {
                // Show the activity indicator if the map is drawing
                if (e.Status == DrawStatus.InProgress)
                {
                    ActivityIndicator.IsEnabled = true;
                    ActivityIndicator.Visibility = Visibility.Visible;
                }
                else
                {
                    ActivityIndicator.IsEnabled = false;
                    ActivityIndicator.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void MyMapView_GeoViewTapped(object sender, Esri.ArcGISRuntime.UI.Controls.GeoViewInputEventArgs e)
        {
            var mapClickPoint = e.Location;

            //var stringPoint = string.Format("X = {0}, Y = {1}", mapClickPoint.X, mapClickPoint.Y);
            //MessageBox.Show(stringPoint);

            //AddSimpleMarker(mapClickPoint);
            //AddPictureMarker(mapClickPoint);
            //AddCallOut(mapClickPoint);
        }

        private void AddCallOut(MapPoint mapClickPoint)
        {
            // Project the user-tapped map point location to a geometry
            Geometry myGeometry = GeometryEngine.Project(mapClickPoint, SpatialReferences.Wgs84);

            // Convert to geometry to a traditional Lat/Long map point
            MapPoint projectedLocation = (MapPoint)myGeometry;

            // Format the display callout string based upon the projected map point (example: "Lat: 100.123, Long: 100.234")
            string mapLocationDescription = string.Format("Lat: {0:F3} Long:{1:F3}", projectedLocation.Y, projectedLocation.X);

            // Create a new callout definition using the formatted string
            CalloutDefinition myCalloutDefinition = new CalloutDefinition("Location:", mapLocationDescription);

            // Display the callout
            MyMapView.ShowCalloutAt(mapClickPoint, myCalloutDefinition);
        }

        private async void AddPictureMarker(MapPoint mapClickPoint)
        {
            // Add graphics using different source types
            //CreatePictureMarkerSymbolFromUrl(overlay, mapClickPoint);
            await CreatePictureMarkerSymbolFromResources(overlay, mapClickPoint);

        }

        private void CreatePictureMarkerSymbolFromUrl(GraphicsOverlay overlay, MapPoint mapClickPoint)
        {
            // Create uri to the used image
            var symbolUri = new Uri("http://sampleserver6.arcgisonline.com/arcgis/rest/services/Recreation/FeatureServer/0/images/e82f744ebb069bb35b234b3fea46deae");

            // Create new symbol using asynchronous factory method from uri.
            PictureMarkerSymbol campsiteSymbol = new PictureMarkerSymbol(symbolUri)
            {
                Width = 40,
                Height = 40
            };                        

            // Create graphic with the location and symbol
            Graphic campsiteGraphic = new Graphic(mapClickPoint, campsiteSymbol);

            // Add graphic to the graphics overlay
            overlay.Graphics.Add(campsiteGraphic);
        }

        private async Task CreatePictureMarkerSymbolFromResources(GraphicsOverlay overlay, MapPoint mapClickPoint)
        {
            // Get current assembly that contains the image
            var currentAssembly = Assembly.GetExecutingAssembly();

            // Get image as a stream from the resources
            // Picture is defined as EmbeddedResource and DoNotCopy
            var resourceStream = currentAssembly.GetManifestResourceStream(
                "ArcgisPoC.placeholder.png");

            // Create new symbol using asynchronous factory method from stream
            PictureMarkerSymbol pinSymbol = await PictureMarkerSymbol.CreateAsync(resourceStream);
            pinSymbol.Width = 50;
            pinSymbol.Height = 50;
                       

            // Create graphic with the location and symbol
            Graphic pinGraphic = new Graphic(mapClickPoint, pinSymbol);

            // Add graphic to the graphics overlay
            overlay.Graphics.Add(pinGraphic);
        }

        private void AddSimpleMarker(MapPoint mapClickPoint)
        {
            // Create overlay to where graphics are shown
            GraphicsOverlay overlay = new GraphicsOverlay();

            // Add created overlay to the MapView
            MyMapView.GraphicsOverlays.Add(overlay);

            // Create a simple marker symbol
            SimpleMarkerSymbol simpleSymbol = new SimpleMarkerSymbol()
            {
                Color = System.Drawing.Color.Red,
                Size = 10,
                Style = SimpleMarkerSymbolStyle.Circle
            };

            // Add a new graphic with a central point that was created earlier
            Graphic graphicWithSymbol = new Graphic(mapClickPoint, simpleSymbol);
            overlay.Graphics.Add(graphicWithSymbol);
        }

        private void MyMapView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry) == null)
                return;

            System.Windows.Point screenPoint = e.GetPosition(MyMapView);
            ScreenCoordsTextBlock.Text = string.Format("Screen Coords: X = {0}, Y = {1}",
                screenPoint.X, screenPoint.Y);

            MapPoint mapPoint = MyMapView.ScreenToLocation(screenPoint);
            if (MyMapView.WrapAroundMode == Esri.ArcGISRuntime.UI.WrapAroundMode.EnabledWhenSupported)
                mapPoint = GeometryEngine.NormalizeCentralMeridian(mapPoint) as MapPoint;
            MapCoordsTextBlock.Text = string.Format("Map Coords: X = {0}, Y = {1}",
                    Math.Round(mapPoint.X, 4), Math.Round(mapPoint.Y, 4));
        }

        private void OnBasemapChooserSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Get the title of the selected basemap
            var selectedBasemapTtile = e.AddedItems[0].ToString();

            // Retrieve the basemap from the dictionary
            MyMapView.Map.Basemap = basemapOptions[selectedBasemapTtile];
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MyMapView.Map.MaxScale = double.Parse(txtMaxEscale.Text);
            MyMapView.Map.MinScale = double.Parse(txtMinEscale.Text);
        }
        
        private void Button_Click_Magnifier(object sender, RoutedEventArgs e)
        {
            //This sample only works on a device with a touch screen.The magnifier will not appear via a mouse click.
            MyMapView.InteractionOptions.IsMagnifierEnabled = chkMagnifier.IsChecked.Value;
        }
    }
}
