using System;
using System.IO;
using System.Net.Mime;

using MassTransit;
using MassTransit.BusConfigurators;
using MassTransit.Context;
using MassTransit.EndpointConfigurators;
using MassTransit.NLogIntegration;
using MassTransit.Serialization;
using MassTransit.Util;

using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace Demo.MassTransitFaultHandling
{
    public interface FaultyCommand
    {
        string Id { get; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var bus = ServiceBusFactory.New(x =>
            {
                x.UseRabbitMq();
                x.ReceiveFrom("rabbitmq://localhost/test/faulty_consumer");
                x.SetDefaultRetryLimit(1);

                x.Subscribe(s => s.Handler<FaultyCommand>((context, message) =>
                {
                    Console.WriteLine("Command: {0}", message.Id);
                    throw new InvalidOperationException("Expected to fail: " + message.Id);
                }));

                x.Subscribe(s => s.Handler<Fault<FaultyCommand>>(Handle));
                x.Subscribe(s => s.Handler<IFault>(Handle));
            });

            using (bus)
            {
                var faultBus = ServiceBusFactory.New(x =>
                {
                    x.UseRabbitMq();
                    x.ReceiveFrom("rabbitmq://localhost/test/fault_monitor");

                    x.Subscribe(s => s.Handler<Fault<FaultyCommand>>(Handle));
                    x.Subscribe(s => s.Handler<IFault>(Handle));
                });

                using (faultBus)
                {
                    bus.Endpoint.Send<FaultyCommand>(new
                    {
                        Id = "12345"
                    });

                    Console.ReadKey();
                    Console.WriteLine("Exiting...");
                }
            }
        }

        private static void ConfigureNLog(ServiceBusConfigurator x)
        {
            x.UseNLog();
        }

        private static void ConfigureJsonDeserializer(ServiceBusConfigurator x)
        {
            // *Article on handling serialization faults and possibility of mt3 on handling that.
            // https://groups.google.com/forum/#!searchin/masstransit-discuss/serialization/masstransit-discuss/0l8dOD08CDg/oOnRZEI_CgAJ

            x.ConfigureJsonDeserializer(settings =>
            {
                settings.Error = HandleErrors;
                return settings;
            });
        }

        static void Handle<T>(IConsumeContext<T> context, T message) where T : class
        {
            var format = "{0} -- {1}";
            var path = context.Endpoint.Address.Uri.PathAndQuery;
            var typeName = typeof (T).Name == "IFault"
                ? $"IFault::{((IFault) message).FaultType}"
                : $"{typeof (T).GetTypeName()}";
            var value = string.Format(format, path, typeName);

            Console.WriteLine(value);
        }

        static void HandleErrors(object sender, ErrorEventArgs errorArgs)
        {
            var currentError = errorArgs.ErrorContext.Error.Message;
            errorArgs.ErrorContext.Handled = true;
        }

        private static void TestDeserializationErrors()
        {
            var value = JsonConvert.DeserializeObject("{}{}", new JsonSerializerSettings
            {

                Error = (sender, errorArgs) =>
                {
                    var currentError = errorArgs.ErrorContext.Error.Message;
                    errorArgs.ErrorContext.Handled = true;
                }
            });
        }
    }
}
