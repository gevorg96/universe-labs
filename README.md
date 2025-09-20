# Лаба 2

### RabbitMQ
[Оф. сайт](https://www.rabbitmq.com/)

1) Дописываем в файл `docker-compose.yml`:
```yaml
### ...
    rabbitmq:
        image: rabbitmq:3.13-management-alpine
        container_name: rabbitmq
        ports:
            - "5672:5672"
            - "15672:15672"
        volumes:
            - rabbitmq_data:/var/lib/rabbitmq

volumes:
    pgdata:
    rabbitmq_data:
```

2) Запускаем `docker-compose up -d`. Теперь у нас должно быть развернуто 3 контейнера.
3) Переходим по ссылке http://localhost:15672. Должна открыться админ-панель RabbitMQ. Логин / пароль: `guest / guest`


4) В первой итерации займемся издателем (паблишером/продьюсером)
4) Переходим к проекту UniverseLabs.Oms. Подключаем пакет `RabbitMQ.Client 7.1.2`.
   Это позволит нам публиковать событие в очереди.
5) В appsettings.Development.json пишем настройки RabbitMQ:
```json
"RabbitMqSettings": {
    "HostName": "localhost",
    "Port": 5672,
}
```
6) Создаем в корне проекта папку Config и в нее добавляем файл RabbitMqSettings.cs.
   В нем должно быть два свойства: `string HostName` и `int Port`.
7) В файле Program.cs пишем код:
```csharp
    builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(nameof(RabbitMqSettings)));
```
Теперь мы из любой части проекта можем получить доступ к настройкам подключения к RabbitMQ.
8) Далее нужно позаниматься инфраструктурой. Для начала создадим два проекта типа ClassLibrary в солюшне `UniverseLabs.Common` и `UniverseLabs.Messages`.
   В проекте `UniverseLabs.Common` подключаем nuget Newtonsoft.Json и создаем один единственный класс JsonSerializeExtensions.
```csharp
public static class JsonSerializeExtensions
{
    private static readonly JsonSerializerSettings Formatter = new()
    {
        Formatting = Formatting.Indented,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        },
        NullValueHandling = NullValueHandling.Ignore,
        Converters = new List<JsonConverter>
        {
            new StringEnumConverter()
        }
    };
    
    public static string ToJson<T>(this T obj) => JsonConvert.SerializeObject(obj, Formatter);
    
    public static T FromJson<T>(this string json) => JsonConvert.DeserializeObject<T>(json, Formatter)!;
}
```
Это позволит нам легко и просто сериализовать/десериализовать объекты в JSON/из JSON.
9) В проекте `UniverseLabs.Messages` создаем класс `OrderCreatedMessage`, который по наполеннию свойств должен быть идентичен классу из проекта `UniverseLabs.Oms.Models.Dto.Common.OrderUnit.cs`.


10) Далее переходим в проект `UniverseLabs.Oms` и в папке Services создаем класс `RabbitMqService`.
    Его единственная зависимость - `IOptions<RabbitMqSettings>`.
> NB!
>
> Чтобы подключиться к RabbitMQ нужно создать фабрику подключений, подключение и канал подключения.
> Все это есть в библиотеке, которую вы подключили ранее.
>
> 1. RabbitMQ.Client.ConnectionFactory - фабрика подключений
>
> 2. RabbitMQ.Client.IConnection - подключение
>
> 3. RabbitMQ.Client.IChannel - канал подключения
>
> Из фабрики мы получаем экземпляр IConnection, из экземпляра IConnection мы получаем экземпляр IChannel.
> После получения экземпляра IChannel мы можем убедиться в наличии очереди (и если ее нет, то создать ее) и публиковать сообщения в очередь.

Выглядит класс RabbitMqService следующим образом:
```csharp
public class RabbitMqService(IOptions<RabbitMqSettings> settings)
{
    private readonly ConnectionFactory _factory = new() { HostName = settings.Value.HostName, Port = settings.Value.Port };
    
    public async Task Publish<T>(IEnumerable<T> enumerable, string queue, CancellationToken token)
    {
        await using var connection = await _factory.CreateConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
        await channel.QueueDeclareAsync(
            queue: queue, 
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: token);

        foreach (var message in enumerable)
        {
            var messageStr = message.ToJson();
            var body = Encoding.UTF8.GetBytes(messageStr);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queue,
                body: body,
                cancellationToken: token);
        }
    }
}
```

О параметрах метода QueueDeclareAsync вы можете узнать из лекции или из оф. источника.
Коротко: метод проверяет наличие очереди в брокере и если ее нет, то создает ее.

> Ремарка по поводу публикации событий: все события публикуются как массив байтов,
поэтому любое ваше сообщение должно быть сериализовано в строчку, из которой вы получите byte-array. Забегая наперед, чтение событий по подписке происходит аналогично - получаем byte-array и десериализуем его.

11) Далее регистрируем RabbitMqService в контейнере зависимостей в файле `Program.cs`.


12) Также нужно понять, какие очереди мы будем делать. Для начала предлагается создать очередь `oms.order.created`.
    - Добавляем в appsettings.Development.json в секцию `RabbitMqSettings` новое поле `"OrderCreatedQueue" : "oms.order.created"`.
    - В файл `RabbitMqSettings.cs` добавляем новое поле `OrderCreatedQueue`.


13) Теперь мы можем внедрить наши зависимости в класс `OrderService`. Добавляем зависимости `RabbitMqService` и `IOptions<RabbitMqSettings>`.


14) Перед тем как вернуть значение из метода `BatchInsert` класса `OrderService` собираем массив объектов `OmsOrderCreatedMessage` и вызываем метод Publish внедренного сервиса следующим образом:
```csharp
await _rabbitMqService.Publish(messages, settings.Value.OrderCreatedQueue, cancellationToken);
```
15) Запускаем проект. Пытаемся вызвать ручку api/v1/order/batch-create. После получения успешного ответа идем в админку rabbitmq и в очереди `oms.order.created` должны появиться сообщения.
    Для этого проваливаемся внутрь очереди и жмем кнопку `Get Message(s)`.
    ![admin1.png](admin1.png)
    ![admin2.png](admin2.png)


16) Поздравляем! Вы опубликовали свое первое сообщение в RabbitMQ.


17) Пришло время заняться подписчиком (сабскрайбер/консьюмер).
    Для начала озадачим себя: что мы можем сделать асинхронно?
    Как пример, в крупных банках (и не только) часто делают такую вещь как аудит логи.
    Запись в аудито лог должна быть неблокирующей для любого http-запроса, поэтому аудит-логи чаще всего пишут асинхронно.
    Т.к. студенты к текущему этапу обучения уже умеют создавать ручки от контроллеров до БД, то DAL/BLL/Controller для аудит лога заказов необходимо реализовать самостоятельно.

> Подсказка: от вас требуется написать миграцию, добавить DAL объект, замапить тип постгреса в UnitOfWork, написать репозиторий, сервис, контроллер, валидатор, ну и не забыть, где хранятся реквесты/респонсы.

В нашем же случае будет табличка вида:
```sql
create table if not exists audit_log_order (
    id bigserial not null primary key,
    order_id bigint not null,
    order_item_id bigint not null,
    customer_id bigint not null,
    order_status text not null,
    created_at timestamp with time zone not null,
    updated_at timestamp with time zone not null
);
```

А ручка тем временем будет принимать реквест вида:
```csharp
public class V1AuditLogOrderRequest
{
    public LogOrder[] Orders { get; set; }
    
    public class LogOrder
    {
        public long OrderId { get; set; }
    
        public long OrderItemId { get; set; }
    
        public long CustomerId { get; set; }
    
        public string OrderStatus { get; set; }
    }
}
```

18) Далее переходим к консьюмеру. Для этого создадим новый пустой проект WebApi в том же солюшне - `UniverseLabs.Oms.Consumer`.
    Удалим из него все лишние папки, должны остаться только Properties, appsettings.Development.json, appsettings.json и Program.cs.
19) В appsettings.Development.json пишем те же самые настройки RabbitMQ и добавим еще настройки для httpClient-а Oms:
```json
{
    "RabbitMqSettings": {
        "HostName": "localhost",
        "Port": 5672,
        "OrderCreatedQueue" : "oms.order.created"
    },
    "HttpClient": {
        "Oms": {
          "BaseAddress": "http://localhost:5000"
        }
    }
}
```

20) Создаем аналогичную папку Config в корне проекта и в нее копируем файл RabbitMqSettings.cs из проекта `UniverseLabs.Oms`.
21) Теперь надо создать httpClient. Для этого создаем папку в корне проекта Clients, в нее добавляем файл `OmsClient.cs`:
```csharp
public class OmsClient(HttpClient client)
{
    public async Task<V1AuditLogOrderResponse> LogOrder(V1AuditLogOrderRequest request, CancellationToken token)
    {
        var msg = await client.PostAsync("api/v1/audit/log-order", new StringContent(request.ToJson(), Encoding.UTF8, "application/json"), token);
        if (msg.IsSuccessStatusCode)
        {
            var content = await msg.Content.ReadAsStringAsync(cancellationToken: token);
            return content.FromJson<V1AuditLogOrderResponse>();
        }

        throw new HttpRequestException();
    }
}
```
api/v1/audit/log-order - это ручка, которую мы создали в проекте `UniverseLabs.Oms` в шаге №17.

22) Можно заметить, что наш ToJson/FromJson использует сериализацию в snake_case, но в сваггере у нас почему-то camelCase. Чтобы это исправить, надо
    в проекте UniverseLabs.Oms изменить в файл Program.cs builder.Services.AddControllers() следующим образом:
```csharp
builder.Services.AddControllers().AddJsonOptions(options => 
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});
```
Тут мы просто задаем политику сериализации всех ручек сервиса.

23) Далее возвращаемся в проект `UniverseLabs.Oms.Consumer`. Создадим папку Consumers. В ней создадим файл OmsOrderCreatedConsumer.cs:
```csharp
public class OmsOrderCreatedConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<RabbitMqSettings> _rabbitMqSettings;
    private readonly ConnectionFactory _factory;
    private IConnection _connection;
    private IChannel _channel;
    private AsyncEventingBasicConsumer _consumer;
    
    public OmsOrderCreatedConsumer(IOptions<RabbitMqSettings> rabbitMqSettings, IServiceProvider serviceProvider)
    {
        _rabbitMqSettings = rabbitMqSettings;
        _serviceProvider = serviceProvider;
        _factory = new ConnectionFactory { HostName = rabbitMqSettings.Value.HostName, Port = rabbitMqSettings.Value.Port };
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await _factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(
            queue: _rabbitMqSettings.Value.OrderCreatedQueue, 
            durable: false, 
            exclusive: false,
            autoDelete: false,
            arguments: null, 
            cancellationToken: cancellationToken);

        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.ReceivedAsync += async (sender, args) =>
        {
            var body = args.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var order = message.FromJson<OmsOrderCreatedMessage>();

            Console.WriteLine("Received: " + message);
            
            using var scope = _serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<OmsClient>();
            await client.LogOrder(new V1AuditLogOrderRequest
            {
                Orders = order.OrderItems.Select(x => 
                    new V1AuditLogOrderRequest.LogOrder
                    {
                        OrderId = order.Id,
                        OrderItemId = x.Id,
                        CustomerId = order.CustomerId,
                        OrderStatus = nameof(OrderStatus.Created)
                    }).ToArray()
            }, CancellationToken.None);
        };
        
        await _channel.BasicConsumeAsync(
            queue: _rabbitMqSettings.Value.OrderCreatedQueue, 
            autoAck: true, 
            consumer: _consumer,
            cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        _connection?.Dispose();
        _channel?.Dispose();
    }
}
```

А теперь всё по порядку:
- Внедряем RabbitMqSettings - тут все понятно
- Создаем фабрику подключений к RabbitMQ
- Внедряем контейнер зависимостей (который ServiceLocator)
- В методе StartAsync создаем подключение и канал к RabbitMQ
- Убеждаемся в наличии очереди
- Создаем экземпляр консьюмера
- Подписываемся на событие ReceivedAsync
- Начинаем слушать события в BasicConsumeAsync

Как вы можеже заметить, сообщение читается как byte-array, а потом десериализуется в объект.
Далее из контейнера создается scope (дабы все зависимости были новыми при резолве) и получается завимость OmsClient.
Вызываем метод LogOrder у OmsClient, который в свою очередь отправляет запрос в UniverseLabs.Oms в нужную нам ручку.

25) В Program.cs пишем следующий код:
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(nameof(RabbitMqSettings)));
builder.Services.AddHostedService<OmsOrderCreatedConsumer>();
builder.Services.AddHttpClient<OmsClient>(c => c.BaseAddress = new Uri(builder.Configuration["HttpClient:Oms:BaseAddress"]));

var app = builder.Build();
await app.RunAsync();
```

26) В конфигурационном файле Properties/launchSettings.json мапимся на 5001 порт, чтобы не было пересечений по занятым портам на вашем ПК.

27) Все готово!

Запускаем проекты UniverseLabs.Oms и UniverseLabs.Oms.Consumer и в браузере открываем http://localhost:5000/swagger.
Вызываем ручку создания заказа и ждем пару секунд. После этого посмотрите в БД - должна появиться запись в таблице audit_log_order в статусе Created.

> Вы можете запустить два проекта в Rider-e, если нажмете правой кнопкой мыши на проект -> Run поочередно.
> Также можно все это проделать через консоль/терминал: переходим в папку с проектом и пишем `dotnet run` - вам понадобиться два окна с терминалом.

28) На этом всё! На следующем уровне откроется возможность коммитить корректную обработку сообщений
    и в случае ошибки не делать этого. Также мы посмотрим, как rabbitmq балансирует между несколькими консьюмерами одной и той же очереди!