using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CSharpCrawler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IWebDriver driver;
        string saveDir;
        int count = 0; // image count  
        long jpegByteSize; // image file size
        int numberOfImagesToScroll = 0;

        private static BackgroundWorker mainService = new BackgroundWorker();

        enum steps
        {
            openBrowser,
            loadingImages,
            downloading
        }

        public MainWindow()
        {
            InitializeComponent();

            mainService.DoWork += new DoWorkEventHandler(mainService_doWork);
            mainService.ProgressChanged += new ProgressChangedEventHandler(mainService_ProgressChanged);
            mainService.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mainService_WorkerCompleted);
            mainService.WorkerReportsProgress = true;
            mainService.WorkerSupportsCancellation = true;
        }

        private void Btn_download_Click(object sender, RoutedEventArgs e)
        {
            if (!mainService.IsBusy)
                mainService.RunWorkerAsync(TB_searchContent.Text);
        }

        private void mainService_doWork(object sender, DoWorkEventArgs e)
        {
            var content = (string)e.Argument;

            mainService.ReportProgress((int)steps.openBrowser);
            driver = initBrowser();
            saveDir = makeDir(content);
            search(driver, content);
            mainService.ReportProgress((int)steps.loadingImages);
            findAndSaveImg(driver, content, saveDir);
        }

        private void mainService_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch (e.ProgressPercentage)
            {
                case (int)steps.openBrowser:
                    LB_result.Items.Add($"Opening browser...");
                    break;
                case (int)steps.loadingImages:
                    LB_result.Items.Add($"loading images...");
                    break;
                case (int)steps.downloading:
                    LB_result.Items.Add($"Downloading image {count}\tsize: {jpegByteSize / 1024}KB");
                    break;
            }
            mScrollViewer.ScrollToBottom();
        }

        private void mainService_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

        }


        private IWebDriver initBrowser()
        {
            var chromeDriverService = ChromeDriverService.CreateDefaultService(@"C:\Program Files (x86)\Google\Chrome\Application");
            chromeDriverService.HideCommandPromptWindow = true;
            return new ChromeDriver(chromeDriverService, new ChromeOptions());
        }

        private void search(IWebDriver _driver, string _searchContent)
        {
            string searchContent = _searchContent.Replace(" ", "+");
            string url = "https://www.google.com/search?q=" + searchContent + "&source=lnms&tbm=isch";
            _driver.Navigate().GoToUrl(url);
            _driver.Manage().Window.Maximize();
        }

        private string makeDir(string _searchContent)
        {
            string dir = Path.Combine(Environment.CurrentDirectory, _searchContent);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void findAndSaveImg(IWebDriver _driver, string _searchContent, string _dirName)
        {
            List<string> image_url_dic = new List<string>();
            string thumbnailXpath = "//img[@class='rg_ic rg_i']";
            string showMoreButtonXpath = "//input[@id='smb']";
            ReadOnlyCollection<IWebElement> imageHolders;

            IWait<IWebDriver> wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30.00));
            int waitToLoad = 4000;
            /// Init the image holder list
            try
            {
                imageHolders = _driver.FindElements(By.XPath(thumbnailXpath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return;
            }

            /// Scroll to the end of the page to load all images
            IWebElement showMoreButton;// = _driver.FindElement(By.XPath(showMoreButtonXpath));
            bool IsSMBVisible = false;

            
            int maxNumber = imageHolders.Count();
            while (true)
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");

                /// Wait for loading complete
                Thread.Sleep(waitToLoad);
                //wait.Until(_driver1 => ((IJavaScriptExecutor)_driver).ExecuteScript("return document.readyState").Equals("complete"));

                if (TryFindElement(By.XPath(showMoreButtonXpath), out showMoreButton))
                {
                    IsSMBVisible = IsElementVisible(showMoreButton);
                }
                if (IsSMBVisible)
                {
                    showMoreButton.Click();
                    Thread.Sleep(waitToLoad);
                    //wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
                    
                }

                imageHolders = _driver.FindElements(By.XPath(thumbnailXpath));
                if (imageHolders.Count() > maxNumber)
                    maxNumber = imageHolders.Count();
                else
                    break;
            }

            /// Download all images;
            //var imageHolders = _driver.FindElements(By.XPath(thumbnailXpath));

            foreach (var imageHolder in imageHolders)
            {
                var img_url = imageHolder.GetAttribute("src");
                if (img_url == null) continue;
                try
                {
                    if (img_url.Contains("data:image"))
                    {
                        var base64Data = Regex.Match(img_url, @"data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
                        var binData = Convert.FromBase64String(base64Data);
                        using (var stream = new MemoryStream(binData))
                        {
                            Bitmap bitmap = new Bitmap(stream);
                            if (bitmap != null)
                            {
                                bitmap.Save(Path.Combine(_dirName, $"pic{count:D5}.jpg"), ImageFormat.Jpeg);
                                jpegByteSize = stream.Length;
                            }
                        }
                    }
                    else if (img_url.Contains("https://") || img_url.Contains("http://"))
                    {
                        using (WebClient client = new WebClient())
                        {
                            Stream stream = client.OpenRead(img_url);
                            Bitmap bitmap;
                            bitmap = new Bitmap(stream);
                            if (bitmap != null)
                            {
                                bitmap.Save(Path.Combine(_dirName, $"pic{count:D5}.jpg"), ImageFormat.Jpeg);
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    bitmap.Save(ms, ImageFormat.Jpeg);
                                    ms.Close();

                                    jpegByteSize = ms.ToArray().LongLength;
                                }
                            }
                            stream.Flush();
                            stream.Close();
                        }
                    }

                    count++;
                    if (count > numberOfImagesToScroll - 1)
                        break;
                    mainService.ReportProgress((int)steps.downloading);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                Thread.Sleep(50);
            }

        }

        public bool TryFindElement(By by, out IWebElement element)
        {
            try
            {
                element = driver.FindElement(by);
            }
            catch (NoSuchElementException ex)
            {
                element = null;
                return false;
            }
            return true;
        }

        public bool IsElementVisible(IWebElement element)
        {
            return element.Displayed && element.Enabled;
        }

        private void TB_numPages_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            numberOfImagesToScroll = Convert.ToInt32(TB_numImages.Text);
        }

        private void Btn_close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Btn_minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}
