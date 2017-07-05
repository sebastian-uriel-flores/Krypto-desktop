using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers.hamming;
using FilesEncryptor.pages;
using Krypto.viewmodels;
using Krypto.viewmodels.hamming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FilesEncryptor
{
    /// <summary>
    /// Proporciona un comportamiento específico de la aplicación para complementar la clase Application predeterminada.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Inicializa el objeto de aplicación Singleton. Esta es la primera línea de código creado
        /// ejecutado y, como tal, es el equivalente lógico de main() o WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;        
        }

        private void App_BackRequested(object sender, BackRequestedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
                return;

            // Navigate back if possible, and if the event has not 
            // already been handled .
            if (rootFrame.CanGoBack && e.Handled == false)
            {
                e.Handled = true;
                rootFrame.GoBack();
            }
        }

        /// <summary>
        /// Function to convert Hex to Color
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public SolidColorBrush GetSolidColorBrush(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = (byte)(Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte r = (byte)(Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte g = (byte)(Convert.ToUInt32(hex.Substring(4, 2), 16));
            byte b = (byte)(Convert.ToUInt32(hex.Substring(6, 2), 16));
            SolidColorBrush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            return myBrush;
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            base.OnFileActivated(args);
            IReadOnlyList<IStorageItem> items = args.Files;

            bool decode = false;

            foreach (StorageFile item in items)
            {
                decode = false;
                foreach (HammingEncodeType encodeType in BaseHammingCodifier.EncodeTypes)
                {
                    if (encodeType.Extension.Equals(item.FileType))
                    {
                        decode = true;
                        break;
                    }
                }
            }

            if (decode)
            {
                ActivateFrame(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, new HammingDecodeViewModel() }, { ProcessPage.ARGS_PARAM, items }, { ProcessPage.APP_ACTIVATED_ARGS, true } });
            }
            else
            {
                ActivateFrame(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, new HammingEncodeViewModel() }, { ProcessPage.ARGS_PARAM, items }, { ProcessPage.APP_ACTIVATED_ARGS, true } });
            }
        }

        protected override async void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            base.OnShareTargetActivated(args);
            IReadOnlyList<IStorageItem> items = await args.ShareOperation.Data.GetStorageItemsAsync();

            bool decode = false;

            foreach(StorageFile item in items)
            {
                decode = false;
                foreach(HammingEncodeType encodeType in BaseHammingCodifier.EncodeTypes)
                {
                    if(encodeType.Extension.Equals(item.FileType))
                    {
                        decode = true;
                        break;
                    }
                }
            }

            if (decode)
            {
                ActivateFrame(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, new HammingDecodeViewModel() }, { ProcessPage.ARGS_PARAM, items }, { ProcessPage.APP_ACTIVATED_ARGS, true } });
            }
            else
            {
                ActivateFrame(typeof(ProcessPage), new Dictionary<string, object>() { { ProcessPage.VIEW_MODEL_PARAM, new HammingEncodeViewModel() }, { ProcessPage.ARGS_PARAM, items }, { ProcessPage.APP_ACTIVATED_ARGS, true } });
            }
        }

        private async void ActivateFrame(Type typeOfPage, Dictionary<string,object> args)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if(rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;

                // Poner el marco en la ventana actual.
                Window.Current.Content = rootFrame;

                rootFrame.Navigate(typeOfPage, args);

                SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
                // Asegurarse de que la ventana actual está activa.
                Window.Current.Activate();

                SetUpTitleBar();
            }
            else
            {
                CoreApplicationView _newView = CoreApplication.CreateNewView();
                
                int newViewId = 0;
                await _newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frame newFrame = new Frame();

                    newFrame.NavigationFailed += OnNavigationFailed;

                    SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;

                    // Poner el marco en la ventana actual.
                    Window.Current.Content = newFrame;

                    newFrame.Navigate(typeOfPage, args);

                    SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
                    // Asegurarse de que la ventana actual está activa.
                    Window.Current.Activate();

                    newViewId = ApplicationView.GetForCurrentView().Id;

                    SetUpTitleBar();
                });
                bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
            }
        }

        private void SetUpTitleBar()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    var mainOrange = GetSolidColorBrush("#FFFB8300").Color;
                    var secondOrange = GetSolidColorBrush("#FFCD3927").Color;

                    titleBar.ButtonBackgroundColor = mainOrange;
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.ButtonHoverBackgroundColor = secondOrange;
                    titleBar.ButtonInactiveBackgroundColor = mainOrange;
                    titleBar.ButtonInactiveForegroundColor = Colors.White;

                    titleBar.BackgroundColor = mainOrange;
                    titleBar.ForegroundColor = Colors.White;
                    titleBar.InactiveBackgroundColor = mainOrange;
                    titleBar.InactiveForegroundColor = Colors.White;
                }
            }
        }

        /// <summary>
        /// Se invoca cuando el usuario final inicia la aplicación normalmente. Se usarán otros puntos
        /// de entrada cuando la aplicación se inicie para abrir un archivo específico, por ejemplo.
        /// </summary>
        /// <param name="e">Información detallada acerca de la solicitud y el proceso de inicio.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // No repetir la inicialización de la aplicación si la ventana tiene contenido todavía,
            // solo asegurarse de que la ventana está activa.
            if (rootFrame == null)
            {
                // Crear un marco para que actúe como contexto de navegación y navegar a la primera página.
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Cargar el estado de la aplicación suspendida previamente
                }

                SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
                                
                // Poner el marco en la ventana actual.
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // Cuando no se restaura la pila de navegación, navegar a la primera página,
                    // configurando la nueva página pasándole la información requerida como
                    //parámetro de navegación
                    rootFrame.Navigate(typeof(BifurcatorPage), e.Arguments);
                }

                SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
                // Asegurarse de que la ventana actual está activa.
                Window.Current.Activate();
            }

            SetUpTitleBar();
        }
        

        /// <summary>
        /// Se invoca cuando la aplicación la inicia normalmente el usuario final. Se usarán otros puntos
        /// </summary>
        /// <param name="sender">Marco que produjo el error de navegación</param>
        /// <param name="e">Detalles sobre el error de navegación</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Se invoca al suspender la ejecución de la aplicación. El estado de la aplicación se guarda
        /// sin saber si la aplicación se terminará o se reanudará con el contenido
        /// de la memoria aún intacto.
        /// </summary>
        /// <param name="sender">Origen de la solicitud de suspensión.</param>
        /// <param name="e">Detalles sobre la solicitud de suspensión.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Guardar el estado de la aplicación y detener toda actividad en segundo plano
            deferral.Complete();
        }
    }
}
