using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using MQTTnet;

namespace GTKTest
{
    class MainWindow : Window
    {
        static IMqttClient mqttClient;
        [UI] private Label _label1 = null;
        [UI] private Button _button1 = null;
        [UI] private Entry _txtMessage = null;

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private void publish()
        {
            var msg = new MqttApplicationMessageBuilder()
                    .WithTopic("/topic/qos0")
                    //.WithPayload("Hello World " + new Random().Next().ToString() + " ") // + textBox1.Text)
                    .WithPayload(_txtMessage.Text)
                    .WithRetainFlag()
                    .Build();

            mqttClient.PublishAsync(msg);

            Log.Logger.Information("MQTT application message is published.");
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
            _button1.Clicked += Button1_Clicked;

            Log.Logger = new LoggerConfiguration().CreateLogger();

            Log.Logger.ForContext<MqttClient>();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger.Information("Creating pub");
            MqttClientOptions puboptions = new MqttClientOptionsBuilder()
                .WithClientId("AndiMQTTPub")
                .WithTcpServer("darwinistic.com", 1883)
                .WithTimeout(TimeSpan.FromSeconds(60))
                .WithCleanSession()
                .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            mqttClient = new MqttFactory().CreateMqttClient();
            mqttClient.ConnectAsync(puboptions).Wait();

            MqttClientOptionsBuilder optionsbuilder = new MqttClientOptionsBuilder()
                                            .WithClientId("AndiMQTTSub")
                                            .WithTcpServer("darwinistic.com", 1883)
                                            .WithCleanSession(true)
                                            .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                                            .WithTimeout(TimeSpan.FromSeconds(60));

            ManagedMqttClientOptions options = new ManagedMqttClientOptionsBuilder()
                        .WithAutoReconnectDelay(TimeSpan.FromSeconds(60))
                        .WithClientOptions(optionsbuilder.Build())
                        .Build();

            IManagedMqttClient mqttSubClient = new MqttFactory().CreateManagedMqttClient();

            mqttSubClient.ConnectedAsync += mqttClient_ConnectedAsync;
            mqttSubClient.DisconnectedAsync += mqttClient_DisconnectedAsync;
            mqttSubClient.ConnectingFailedAsync += mqttClient_ConnectingFailedAsync;
            mqttSubClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

            mqttSubClient.SubscribeAsync("/topic/qos0");

            mqttSubClient.StartAsync(options).GetAwaiter().GetResult();
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            mqttClient.DisconnectAsync().Wait();
            Application.Quit();
        }

        private void Button1_Clicked(object sender, EventArgs a)
        {
            _label1.Text = string.Format("I will submit message {0}", _txtMessage.Text);
            publish();
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            //Console.WriteLine("Message received: " + arg.ApplicationMessage.ConvertPayloadToString());
            Log.Logger.Information("Message received {0}", arg.ApplicationMessage.ConvertPayloadToString());
            arg.AcknowledgeAsync(CancellationToken.None).Wait();
            arg.IsHandled = true;
            arg.AutoAcknowledge = true;
            return Task.CompletedTask;
        }

        private Task mqttClient_ConnectingFailedAsync(ConnectingFailedEventArgs arg)
        {
            Log.Logger.Information("Connection failed {0}", arg.Exception.Message);
            return Task.CompletedTask;
        }

        private Task mqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Log.Logger.Information("Disconnected {0}", arg.ReasonString);
            return Task.CompletedTask;
        }

        private Task mqttClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Log.Logger.Information("Connection successful {0}", arg.ConnectResult.AssignedClientIdentifier);
            return Task.CompletedTask;
        }

    }
}
