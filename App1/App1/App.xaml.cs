// #define USE_CHEAP_CONTAINER

namespace App1
{
    using App1.Interfaces;
    using Autofac;
    using ConversationLibrary;
    using ConversationLibrary.Interfaces;
    using ConversationLibrary.Utility;
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
#if USE_CHEAP_CONTAINER
                this.BuildContainer();
                Window.Current.Content = new MainPage();
#else
                Window.Current.Content = this.Container.Resolve<MainPage>();
#endif // USE_CHEAP_CONTAINER

            }
            if (!e.PrelaunchActivated)
            {
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }
#if !USE_CHEAP_CONTAINER
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
#endif
        void BuildContainer()
        {
#if USE_CHEAP_CONTAINER
            CheapContainer.Register<ISignallingService, Signaller>();
            CheapContainer.Register<IDispatcherProvider, XamlMediaElementProvider>();
            CheapContainer.Register<IXamlMediaElementProvider, XamlMediaElementProvider>();
            CheapContainer.Register<IMediaManager, XamlMediaElementMediaManager>();
            CheapContainer.Register<IPeerManager, PeerManager>();
            CheapContainer.Register<IConversationManager, ConversationManager>();
#else
            var builder = new ContainerBuilder();
            builder.RegisterType<Signaller>().As<ISignallingService>().SingleInstance();

            builder.RegisterType<XamlMediaElementProvider>().As<IXamlMediaElementProvider>().As<IDispatcherProvider>().SingleInstance();

            builder.RegisterType<XamlMediaElementMediaManager>().As<IMediaManager>().SingleInstance();
            builder.RegisterType<PeerManager>().As<IPeerManager>().SingleInstance();
            builder.RegisterType<ConversationManager>().As<IConversationManager>().SingleInstance();
            builder.RegisterType<MainPage>().AsSelf().SingleInstance();
            this.iocContainer = builder.Build();
#endif
        }
#if USE_CHEAP_CONTAINER
#else
        Autofac.IContainer iocContainer;
#endif
    }
}
