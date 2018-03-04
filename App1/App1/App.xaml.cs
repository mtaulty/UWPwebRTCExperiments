namespace App1
{
    using App1.Interfaces;
    using Autofac;
    using PeerConnectionClient.Interfaces;
    using PeerConnectionClient.Signalling;
    using Windows.ApplicationModel.Activation;
    using Windows.UI.Xaml;

    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var mainPage = Window.Current.Content as MainPage;

            if (mainPage == null)
            {
                Window.Current.Content = this.Container.Resolve<MainPage>();
            }
            if (!e.PrelaunchActivated)
            {
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }
        Autofac.IContainer Container
        {
            get
            {
                if (this.iocContainer == null)
                {
                    this.BuildContainer();
                }
                return (this.iocContainer);
            }
        }
        void BuildContainer()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<Signaller>().As<ISignallingService>().SingleInstance();
            builder.RegisterType<XamlMediaElementProvider>().As<IXamlMediaElementProvider>().SingleInstance();
            builder.RegisterType<XamlMediaElementMediaManager>().As<IMediaManager>().SingleInstance();
            builder.RegisterType<PeerManager>().As<IPeerManager>().SingleInstance();
            builder.RegisterType<MainPage>().AsSelf().SingleInstance();
            this.iocContainer = builder.Build();
        }
        Autofac.IContainer iocContainer;
    }
}
