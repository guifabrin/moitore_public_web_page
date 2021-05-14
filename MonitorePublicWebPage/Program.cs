using mshtml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MonitorePublicWebPage
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        public static extern Icon GetConsoleIcon(IntPtr hWnd);

        private static MenuItem menuItemHideShow;
        private static MenuItem menuItemExit;
        private static bool showHideFlag = false;

        static void Main(string[] args)
        {
            #region arguments
            Dictionary<String, String> argsDict = new Dictionary<string, string>();
            foreach (String arg in args)
            {
                string param = arg.Substring(0, 2);
                if (param == "-H")
                {
                    Console.WriteLine("This is the Helper of MonitorePublicWebPage:");
                    Console.WriteLine("To set URL use: -U=https://site.com where https://site.com is your web page;");
                    Console.WriteLine("To set Which element need to monitor use: -W=A to all page or -W=E to an element;");
                    Console.WriteLine("To set the element use: -E=element where the first character indicate '#' for id and '.' for className;");
                    Console.WriteLine("To set the tries in the internet error use: -T=3;");
                    Console.WriteLine("To set the time in interations use: -P=10 where 10 is in seconds;");
                    return;
                }
                argsDict.Add(param, arg.Substring(3, arg.Length-3));
            }
            #endregion

            #region which is the public URL
            String urlAddress = "";
            while (true)
            {
                Console.Write("Which is the public page URL? ");

                urlAddress = argsDict.ContainsKey("-U") ? argsDict["-U"] : Console.ReadLine();

                if (argsDict.ContainsKey("-U"))
                {
                    Console.WriteLine(urlAddress);
                }
                
                if (CheckURLValid(urlAddress))
                {
                    break;
                } else {
                    if (argsDict.ContainsKey("-U"))
                    {
                        argsDict.Remove("-U");
                    }
                    Console.WriteLine("The url '" + urlAddress + "' is invalid. Try again;");
                }
            }
            #endregion

            #region what i need look in the page
            char whatElem = '\0';
            while (true)
            {
                whatElem = argsDict.ContainsKey("-W") ? argsDict["-W"][0] : '\0';
                if (whatElem != 'A' && whatElem != 'E')
                {
                    if (argsDict.ContainsKey("-W"))
                    {
                        Console.WriteLine("The which element '" + whatElem + "' is invalid. Try again;");
                        argsDict.Remove("-W");
                    }

                    try
                    {
                        Console.Write("All Page (A), Element (E)? ");
                        whatElem = Console.ReadLine()[0];
                        if (whatElem != 'E' && whatElem != 'A')
                        {
                            Console.WriteLine("The which element '" + whatElem + "' is invalid. Try again;");
                        } else
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        //ignore and try again
                    }
                } else
                {
                    if (argsDict.ContainsKey("-W"))
                    {
                        Console.Write("All Page (A), Element (E)? ");
                        Console.WriteLine(whatElem);
                    }
                    break;
                }
            }
            #endregion

            #region what is the element indicator
            String element = null;
            if (whatElem == 'E')
            {
                while (true)
                {
                    element = argsDict.ContainsKey("-E") ? argsDict["-E"] : null;
                    if (element[0] != '#' || element[0] != '.' || element.Length<2)
                    {
                        if (argsDict.ContainsKey("-W"))
                        {
                            Console.WriteLine("The element '" + element + "' is invalid. Try again;");
                            argsDict.Remove("-E");
                        } else
                        {
                            try
                            {
                                Console.Write("Which id (#)id or (.)class of the element? ");
                                element = Console.ReadLine();
                                if (element[0] != '#' || element[0] != '.')
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                //ignore and try again
                            }
                        }
                    } else
                    {
                        if (argsDict.ContainsKey("-E"))
                        {
                            Console.Write("Which id (#)id or (.)class of the element? ");
                            Console.WriteLine(element);
                        }
                        break;
                    }
                }
            }
            #endregion

            #region tries default
            int triesDefault;
            if (!argsDict.ContainsKey("-T") || !int.TryParse(argsDict["-T"], out triesDefault))
            {
                triesDefault = 3;
            }
            #endregion

            #region period default
            int periodDefault;
            if (!argsDict.ContainsKey("-P") || !int.TryParse(argsDict["-P"], out periodDefault))
            {
                periodDefault = 10;
            }
            #endregion

            #region notificationIcon
            new Thread(() =>
            {
                NotifyIcon trayIcon = new NotifyIcon();
                trayIcon.Text = "Monitore Public Web Page";
                trayIcon.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

                ContextMenu trayMenu = new ContextMenu();
                menuItemExit = new MenuItem("Sair");
                menuItemHideShow = new MenuItem("Esconder/Mostrar");
                menuItemHideShow.Click += MenuItemHideShow_Click;
                menuItemExit.Click += MenuItemExit_Click;
                trayMenu.MenuItems.Add(menuItemExit);
                trayMenu.MenuItems.Add(menuItemHideShow);
                trayIcon.ContextMenu = trayMenu;
                trayIcon.Visible = true;
                Application.Run();
            }).Start();

            #endregion

            #region program execution
            int tries = 0;
            string data = null;
            bool changed = false;
            while (true)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Stream receiveStream = response.GetResponseStream();
                        StreamReader readStream = null;

                        if (response.CharacterSet == null)
                        {
                            readStream = new StreamReader(receiveStream);
                        }
                        else
                        {
                            readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                        }

                        string readedData = readStream.ReadToEnd();
                        string selectedData = null;
                        if (whatElem == 'A')
                        {
                            selectedData = readedData;
                        } else
                        {
                            HTMLDocument doc = new HTMLDocument();
                            IHTMLDocument2 doc2 = (IHTMLDocument2)doc;
                            doc2.write(readedData);
                            String indicator = element.Substring(1, element.Length - 1);
                            if (element[0] == '#')
                            {
                                IHTMLElement htmlElem = doc.getElementById(indicator);
                                selectedData = htmlElem.innerHTML;
                            } else
                            {
                                foreach (IHTMLElement htmlElem in doc.all)
                                {
                                    if (htmlElem.getAttribute("className") == indicator)
                                    {
                                        if (htmlElem.id != null)
                                        {
                                            element = "#" + htmlElem.id;
                                        }
                                        selectedData = htmlElem.innerHTML;
                                        break;
                                    }
                                }
                            }
                        }

                        response.Close();
                        readStream.Close();

                        if (data == null)
                        {
                            data = selectedData;
                            Console.WriteLine(DateTime.Now + ": Read data, Chars:" + data.Length + ";");
                        } else
                        {
                            if (data.Length != selectedData.Length)
                            {
                                changed = true;
                                break;
                            } else
                            {
                                Console.WriteLine(DateTime.Now + ": Same; Read data, Chars:" + data.Length + ";");
                            }
                        }
                    }
                    tries = 0;
                } catch (Exception ex)
                {
                    tries++;
                    //ignore and try again
                }
                if (tries >= triesDefault)
                {
                    break;
                }
                Thread.Sleep(periodDefault * 1000);
            }
            #endregion

            if (changed)
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(@"alert.wav");
                player.PlayLooping();
                player.Play();
                Console.WriteLine("There is some alteration. Type any key to open browser...");
                Console.ReadKey();
                System.Diagnostics.Process.Start(urlAddress);
            } else
            {
                Console.WriteLine("There is an error on internet.");
            }

            Console.ReadKey();
        }

        private static void MenuItemExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private static void MenuItemHideShow_Click(object sender, EventArgs e)
        {
            ShowWindow(GetConsoleWindow(), (showHideFlag)?5:0);
            showHideFlag = !showHideFlag;
        }

        public static bool CheckURLValid(string source)
        {
            return Uri.IsWellFormedUriString(source, UriKind.RelativeOrAbsolute);
        }
    }

}
