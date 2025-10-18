# Лаба 4
### Умный роутинг

В целом очереди (queues) - это академическое решение, о котором вам будут рассказывать на парах/курсах на скиллбоксе/etc.
В реальности же мало кто пользуется только ими. В RabbitMQ есть еще обменники (exchanges). Это не очереди,
но весьма похожие на них. Главное отличие - они не хранят сообщения, а перенаправляют их в нужную очередь по определенным правилам.
В текущей лабе мы попробуем понять, как именно их можно применить в продакшене.

Типы обменников:
- Direct
- Topic
- Fanout
- Headers


>Direct exchange маршрутизирует сообщения на основе точного совпадения routing key. Сообщение попадает в очередь только тогда, когда routing key сообщения полностью совпадает с routing key привязки (binding). Это наиболее популярный тип exchange, обладающий необходимой гибкостью для большинства задач.


>Fanout exchange отправляет копию каждого сообщения во все привязанные очереди, полностью игнорируя routing key. Это самый простой тип exchange, который подходит для широковещательной рассылки сообщений.


>Topic exchange использует паттерн-матчинг routing key для маршрутизации сообщений. Поддерживает wildcards:
>
> `*` - совпадение ровно одного сегмента (слова)
>
> `#` - совпадение нуля или более сегментов
>
> Routing key разделяется точками на сегменты. Например, паттерн "regions.na.cities.*" будет соответствовать "regions.na.cities.toronto", но не "regions.na.cities".


>Headers exchange маршрутизирует сообщения на основе заголовков сообщения вместо routing key. При привязке указывается набор заголовков, и сообщение попадает в очередь при соответствии этих заголовков.


Наиболее используемый тип обменника - Topic. Поэтому мы будем использовать его.

1. Для начала создадим новую ручку UpdateOrdersStatus (батчевый апдейт) с таким контрактом:
```csharp
public class V1UpdateOrdersStatusRequest
{
    public long[] OrderIds { get; set; }

    public string NewStatus { get; set; }
}


public class V1UpdateOrderStatusResponse { };
```
Не забудьте, что по переданным OrderIds может не найтись заказов в базе данных. Тогда просто вернем пустой ответ со статусом 200.
Также учтите, что заказы из статуса "created" не могут перейти в статус "completed".
Вам необходимо в ручке реализовать простейшую стейт машину, которая в случае невозможности перехода будет
выбрасывать исключение. Итого ручка может вернуть либо 200, либо 400 (невалидный перевод статуса).

2. После реализации ручки, надо пропатчить продьюсера.
   Теперь мы не можем писать только в очередь oms.order.created, у нас еще должна быть очередь oms.order.status.changed.
   Поэтому меняем `RabbitMqSettings.cs`:
```csharp
public class RabbitMqSettings
{
    public string HostName { get; set; }

    public int Port { get; set; }
    
    public string Exchange { get; set; }
    
    public ExchangeMapping[] ExchangeMappings { get; set; }
    
    public class ExchangeMapping
    {
        public string Queue { get; set; }
        
        public string RoutingKeyPattern { get; set; }
    }
}
```

Что тут изменилось:
- Хост и порт остаются такими же
- Exchange - название вашего обменника
- ExchangeMappings - список очередей и соответствующих им routing key
- OmsOrderCreatedRoutingKey - routing key для событий создания заказа
- OmsOrderUpdatedRoutingKey - routing key для события изменения статуса заказа

3. В `appsettings.Development.json` добавим:
```json
{
    "RabbitMq": {
        "HostName": "localhost",
        "Port": 5672,
        "Exchange": "oms",
        "ExchangeMappings": [
            {
                "Queue": "oms.order.created",
                "RoutingKeyPattern": "order.created"
            },
            {
                "Queue": "oms.order.status.changed",
                "RoutingKeyPattern": "order.status.changed"
            },
            {
                "Queue": "public.oms.order.created",
                "RoutingKeyPattern": "order.created"
            },
            {
                "Queue": "public.oms.order.status.changed",
                "RoutingKeyPattern": "order.status.changed"
            }
        ]
    }
}
```

То есть, если в exchange по имени `oms` опубликовали событие с routing key `order.created`, то оно будет попадать в очереди `oms.order.created` и `public.oms.order.created`.
Для событий с routing key `order.status.changed` - в очереди `oms.order.status.changed` и `public.oms.order.status.changed`.
Публичные очереди в первую очередь нужны для информирования других доменов (например логистики) - заказ создан, логистика начинает его отгружать.

Не забываем заполнить `appsettings.Production.json`.

4. Теперь надо поработать с Messages для продьюсера/консьюмера.
   Создадим абстрактный BaseMessage:
```csharp
public abstract class BaseMessage
{
    public abstract string RoutingKey { get; }
}
```
5. Занаследуемся в OmsOrderCreatedMessage от BaseMessage и реализуем RoutingKey = "order.created".
6. Создадим сообщение OmsOrderStatusChangedMessage. Занаследуемся от BaseMessage и реализуем RoutingKey = "order.status.changed".
   Данные, которые будут передаваться в сообщении, могут быть на ваше усмотрение, но там обязательно должен быть OrderId и OrderStatus.
7. Пропатчим RabbitMqService:
```csharp
public class RabbitMqService(IOptions<RabbitMqSettings> settings) : IDisposable
{
    private readonly ConnectionFactory _factory = new()
    {
        HostName = settings.Value.HostName, 
        Port = settings.Value.Port
    };
    
    private IConnection _connection;
    private IChannel _channel;
    
    private async Task<IChannel> Configure(CancellationToken token)
    {
        if (_channel is not null)
        {
            return _channel;
        }
        
        _connection ??= await _factory.CreateConnectionAsync(token);
        
        _channel = await _connection.CreateChannelAsync(cancellationToken: token);
        await _channel.ExchangeDeclareAsync(settings.Value.Exchange, ExchangeType.Topic, cancellationToken: token);
     
        foreach (var mapping in settings.Value.ExchangeMappings)
        {
            await _channel.QueueDeclareAsync(
                queue: mapping.Queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: token);
            
            await _channel.QueueBindAsync(
                queue: mapping.Queue,
                exchange: settings.Value.Exchange,
                routingKey: mapping.RoutingKeyPattern,
                cancellationToken: token);
        }
        
        return _channel;
    }
    
    public async Task Publish<T>(IEnumerable<T> enumerable, CancellationToken token)
        where T : BaseMessage
    {
        var channel = await Configure(token);

        foreach (var message in enumerable)
        {
            var messageStr = message.ToJson();
            var body = Encoding.UTF8.GetBytes(messageStr);
            await channel.BasicPublishAsync(
                exchange: settings.Value.Exchange,
                routingKey: message.RoutingKey,
                body: body,
                cancellationToken: token);
        }
    }
    
    public void Dispose()
    {
        DisposeConnection();
        GC.SuppressFinalize(this);
    }
    
    ~RabbitMqService()
    {
        DisposeConnection();
    }
    
    private void DisposeConnection()
    {
        _channel?.Dispose();
        _channel = null;
        _connection?.Dispose();
        _connection = null;
    }
}
```

8. Теперь необходимо самостоятельно добавить в OrderService публикацию событий по изменению статуса заказа.
9. Добавим нового консьюмера в UniverseLabs.Oms.Consumer `BatchOmsOrderStatusChangedConsumer`. Он должен вызывать ту же ручку аудит лога, что и BatchOmsOrderCreatedConsumer.
10. Не забудьте добавить в Program.cs следующий кусок кода:
```csharp
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});
```
Без этого не будут работать два консьюмера параллельно.

11. Теперь в базовый консьюмер передать RabbitMqSettings не получится, его тоже надо изменить:
```csharp
public class RabbitMqSettings
{
    public string HostName { get; set; }

    public int Port { get; set; }
    
    public TopicSettingsUnit OrderCreated { get; set; }
    
    public TopicSettingsUnit OrderStatusChanged { get; set; }
    
    public class TopicSettingsUnit
    {
        public string Queue { get; set; }

        public ushort BatchSize { get; set; }

        public int BatchTimeoutSeconds { get; set; }
    }
}
```

А в сам базовый консьюмер передавать не просто `RabbitMqSettings`, но и `Func<RabbitMqSettings, RabbitMqSettings.TopicSettingsUnit>` для извлечения настроек под конкретный топик.
Полагаю, что вы справитесь с этим самостоятельно.

12. После заполните `appsettings.Production.json`/`appsettings.Development.json` согласно новой конфигурации RabbitMqSettings.
13. Допишите OrderGenerator, который будет после создания заказов апдейтить статус у рандомного кол-ва заказов, нам нужна хоть какая-нибудь случайность!
14. Все готово! Запустите приложение и понаблюдайте, что все работает в rabbitmq админ-панели.
15. Убедитесь, что у нас создалось 4 очереди, вместо 2. Также убедитесь в наличии exchange, у которого будут настроены биндинги в определенные очереди.
16. Публичные очереди также должны получать события, это тоже можно проверить в админке.
17. Теперь давайте попробуем в случае ошибки публиковать сообщение в dead letter exchange.
    Для этого надо обновить конфигурацию RabbitMqSettings и в UniverseLabs.Oms.Consumer, и в UniverseLabs.Oms.
    В проекте консьюмера добавим в каждый TopicSettingsUnit поле `DeadLetter`:
```csharp
    public class TopicSettingsUnit
    {
        // ...
        
        public DeadLetterSettings DeadLetter { get; set; }
    }
    
    public class DeadLetterSettings
    {
        public string Dlx { get; set; }

        public string Dlq { get; set; }

        public string RoutingKey { get; set; }
    }
```

И заполним `appsettings.Development.json`:
```json
{
  "RabbitMqSettings": {
    "HostName": "localhost",
    "Port": 5672,
    "OrderCreated": {
      "Queue" : "oms.order.created",
      "BatchSize": 100,
      "BatchTimeoutSeconds": 1,
      "DeadLetter": {
        "Dlx": "oms.order.dlx",
        "Dlq": "oms.order.created.dlq",
        "RoutingKey": "order.created"
      }
    },
    "OrderStatusChanged": {
      "Queue" : "oms.order.status.changed",
      "BatchSize": 100,
      "BatchTimeoutSeconds": 1,
      "DeadLetter": {
        "Dlx": "oms.order.dlx",
        "Dlq": "oms.order.status.changed.dlq",
        "RoutingKey": "order.status.changed"
      }
    }
  }
}
```


В `BaseBatchMessageConsumer` изменим метод `StartAsync`:
```csharp
public async Task StartAsync(CancellationToken token)
    {
        // ...

        await _channel.ExchangeDeclareAsync(
            exchange: _topicSettings.DeadLetter.Dlx,
            type: ExchangeType.Direct,
            durable: true, 
            cancellationToken: token);
        
        await _channel.QueueDeclareAsync(
            queue: _topicSettings.DeadLetter.Dlq,
            durable: true,
            exclusive: false,
            autoDelete: false, 
            cancellationToken: token);
        
        await _channel.QueueBindAsync(
            queue: _topicSettings.DeadLetter.Dlq,
            exchange: _topicSettings.DeadLetter.Dlx,
            routingKey: _topicSettings.DeadLetter.RoutingKey,
            cancellationToken: token);
        
        var queueArgs = new Dictionary<string, object>
        {
            {"x-dead-letter-exchange", _topicSettings.DeadLetter.Dlx},
            {"x-dead-letter-routing-key", _topicSettings.DeadLetter.RoutingKey}
        };
        
        // ...
        
        await _channel.QueueDeclareAsync(
            queue: _topicSettings.Queue, 
            durable: false, 
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs, 
            cancellationToken: token);
        
        // ...
    }
```

А также изменим вызов `BasinNack`, выставим параметр `requeue: false`.
```csharp
await _channel.BasicNackAsync(lastDeliveryTag, multiple: true, requeue: false);
```

18. Аналогично сделаем в RabbitMqSettings в UniverseLabs.Oms.
```json
{
  "RabbitMqSettings": {
    "HostName": "localhost",
    "Port": 5672,
    "Exchange": "oms",
    "ExchangeMappings": [
      {
        "Queue": "oms.order.created",
        "RoutingKeyPattern": "order.created",
        "DeadLetter": {
          "Dlx": "oms.order.dlx",
          "RoutingKey": "order.created"
        }
      },
      {
        "Queue": "oms.order.status.changed",
        "RoutingKeyPattern": "order.status.changed",
        "DeadLetter": {
          "Dlx": "oms.order.dlx",
          "RoutingKey": "order.status.changed"
        }
      },
      {
        "Queue": "public.oms.order.created",
        "RoutingKeyPattern": "order.created"
      },
      {
        "Queue": "public.oms.order.status.changed",
        "RoutingKeyPattern": "order.status.changed"
      },
      {
        "Queue": "oms.logs",
        "RoutingKeyPattern": "order.*"
      }
    ]
  }
}
```

Во время создания exchange/queue в `RabbitMqService.cs` дополнительно заполним аргументы:
```csharp
// ...
        foreach (var mapping in settings.Value.ExchangeMappings)
        {
            var args = mapping.DeadLetter is null ? null : new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", mapping.DeadLetter.Dlx },
                { "x-dead-letter-routing-key", mapping.DeadLetter.RoutingKey }
            };
            
            await _channel.QueueDeclareAsync(
                queue: mapping.Queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                cancellationToken: token);
            
            await _channel.QueueBindAsync(
                queue: mapping.Queue,
                exchange: settings.Value.Exchange,
                routingKey: mapping.RoutingKeyPattern,
                cancellationToken: token);
        }
// ...
```

19. В целях эксперимента, в `BatchOmsOrderCreatedConsumer.cs` изменим логику обработки сообщений:
    Выставим счетчик `counter`, который будет инкрементироваться на каждый приходящий батч. В случае если он кратен 5, то выбросим исключение.

20. Запустим сервис и консьюмера. Убедимся в админке, что `oms.order.created.dlq` заполняется.

21. Задача со звездочкой: добавьте дополнительную очередь `oms.logs`, в которую exchange будет сгружать все события, публикуемые в `oms.order.created` и `oms.order.status.changed`.


На этом наше знакомство с RabbitMQ заканчивается. Далее мы перейдем к другому брокеру сообщений - Apache Kafka.