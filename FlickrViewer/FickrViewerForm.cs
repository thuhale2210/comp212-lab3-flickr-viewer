using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Drawing.Imaging;

namespace FlickrViewer
{
    public partial class FickrViewerForm : Form
    {
        // Use your Flickr API key here--you can get one at:
        // https://www.flickr.com/services/apps/create/apply
        private const string KEY = "c18f861eb9c5ee0111994fc2ab896a03";

        // object used to invoke Flickr web service      
        private static HttpClient flickrClient;

        static FickrViewerForm()
        {
            // Initialize HttpClient without any proxy settings
            flickrClient = new HttpClient(new HttpClientHandler { UseProxy = false });
        }

        Task<string> flickrTask = null; // Task<string> that queries Flickr

        public FickrViewerForm()
        {
            InitializeComponent();
        }

        // initiate asynchronous Flickr search query; 
        // display results when query completes
        private async void searchButton_Click(object sender, EventArgs e)
        {
            // Flickr's web service URL for searches                         
            var flickrURL = "https://api.flickr.com/services/rest/?method=" +
                $"flickr.photos.search&api_key={KEY}&" +
                $"tags={inputTextBox.Text.Replace(" ", ",")}" +
                "&tag_mode=all&per_page=500&privacy_filter=1";

            imagesListBox.DataSource = null; // remove prior data source
            imagesListBox.Items.Clear(); // clear imagesListBox
            pictureBox.Image = null; // clear pictureBox
            imagesListBox.Items.Add("Loading..."); // display Loading...

            try
            {
                // invoke Flickr web service to search Flickr with user's tags
                flickrTask = flickrClient.GetStringAsync(flickrURL);

                // await flickrTask then parse results with XDocument and LINQ
                XDocument flickrXML = XDocument.Parse(await flickrTask);

                // gather information on all photos
                var flickrPhotos =
                    from photo in flickrXML.Descendants("photo")
                    let id = photo.Attribute("id").Value
                    let title = photo.Attribute("title").Value
                    let secret = photo.Attribute("secret").Value
                    let server = photo.Attribute("server").Value
                    let farm = photo.Attribute("farm").Value
                    select new FlickrResult
                    {
                        Title = title,
                        URL = $"https://farm{farm}.staticflickr.com/" +
                          $"{server}/{id}_{secret}.jpg"
                    };

                imagesListBox.Items.Clear(); // clear imagesListBox

                // set ListBox properties only if results were found
                if (flickrPhotos.Any())
                {
                    imagesListBox.DataSource = flickrPhotos.ToList();
                    imagesListBox.DisplayMember = "Title";
                }
                else // no matches were found
                {
                    imagesListBox.Items.Add("No matches");
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                MessageBox.Show($"Request error: {httpRequestException.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }

            // if flickrTask already running, prompt user 
            if (flickrTask?.Status != TaskStatus.RanToCompletion)
            {
                var result = MessageBox.Show(
                   "Cancel the current Flickr search?",
                   "Are you sure?", MessageBoxButtons.YesNo,
                   MessageBoxIcon.Question);

                // determine whether user wants to cancel prior search
                if (result == DialogResult.No)
                {
                    return;
                }
                else
                {
                    flickrClient.CancelPendingRequests(); // cancel search
                }
            }
        }

        // display selected image and generate thumbnail in parallel
        private async void imagesListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (imagesListBox.SelectedItem != null)
            {
                string selectedURL = ((FlickrResult)imagesListBox.SelectedItem).URL;

                try
                {
                    // use HttpClient to get selected image's bytes asynchronously
                    byte[] imageBytes = await flickrClient.GetByteArrayAsync(selectedURL);

                    // Process image and generate thumbnail in parallel
                    Parallel.Invoke(
                        () =>
                        {
                            // Display downloaded image in pictureBox
                            using (var memoryStream = new MemoryStream(imageBytes))
                            {
                                pictureBox.Image = Image.FromStream(memoryStream);
                            }
                        },
                        () =>
                        {
                            // Generate and save thumbnail
                            GenerateThumbnail(imageBytes, "ThumbnailFromFlickr.jpg");
                        }
                    );
                }
                catch (HttpRequestException httpRequestException)
                {
                    MessageBox.Show($"Request error: {httpRequestException.Message}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}");
                }
            }
        }

        private void GenerateThumbnail(byte[] imageBytes, string thumbnailFileName)
        {
            int imageHeight = 100;
            int imageWidth = 100;

            using (var memoryStream = new MemoryStream(imageBytes))
            {
                Image fullSizeImg = Image.FromStream(memoryStream);
                Image.GetThumbnailImageAbort dummyCallBack = new Image.GetThumbnailImageAbort(ThumbnailCallback);
                Image thumbNailImage = fullSizeImg.GetThumbnailImage(imageWidth, imageHeight, dummyCallBack, IntPtr.Zero);
                thumbNailImage.Save(thumbnailFileName, ImageFormat.Jpeg);
                thumbNailImage.Dispose();
                fullSizeImg.Dispose();
            }
        }

        public bool ThumbnailCallback()
        {
            return false;
        }
    }
}
