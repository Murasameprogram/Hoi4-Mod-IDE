# HOI4 Mod IDE 插件间通信规范

**文档版本**：1.0  
**最后更新**：2026-03-04  
**适用主程序版本**：≥ 1.0.0  
**强制标准**：本规范为 HOI4 Mod IDE 插件间通信的**强制性标准**。所有插件若需要与其他插件进行交互，必须严格遵循本文档定义的协议和流程。未遵循本规范的插件将无法保证与其他插件的互操作性，且不被视为符合官方插件开发标准。

---

## 1. 引言

### 1.1 目的
HOI4 Mod IDE 插件系统允许开发者通过独立的插件扩展 IDE 功能。为实现插件间的功能复用与协作，主程序提供了基于消息的路由机制。本文档明确定义了插件间通信的**统一协议**和**强制行为**，确保不同开发者开发的插件能够无缝协作，形成健康的插件生态。

### 1.2 范围
本规范涵盖以下内容：
- 插件间通信的总体架构与消息流转路径
- 服务注册、发现、调用的标准流程
- 直接消息与广播的规则
- 消息格式与错误处理
- 插件开发者必须遵守的最佳实践

---

## 2. 总体架构

### 2.1 核心组件
- **主程序消息路由器**：主程序内部的 `MessageRouter` 组件，维护两个核心数据结构：
  - 插件接收委托表：`Dictionary<string, Action<string>>`，键为插件 ID，值为该插件提供的接收消息委托。
  - 服务映射表：`Dictionary<string, List<string>>`，键为服务名，值为提供该服务的插件 ID 列表。
- **插件通信库**：
  - `HostMessaging`（来自 `HostComms.dll`）：提供插件与主程序通信的底层能力，包括发送消息和接收响应。
  - `PluginMessaging`（来自 `PluginComms.dll`）：在 `HostMessaging` 基础上封装插件间通信的高级 API，包括服务注册、发现、调用、直接消息和广播。

### 2.2 消息流转示意图

<img src="https://github.com/Murasameprogram/Hoi4-Mod-IDE/blob/main/doc/Plugin%20Development%20Guide/插件通信架构图.png" width="600" height="1100">

- **消息路径**：所有插件间消息**必须**经过主程序转发。插件 A 通过其 `PluginMessaging` 发送的消息，先到达主程序路由器，路由器解析目标后转发给插件 B 的接收委托，最终由插件 B 的 `HostMessaging` 处理。

---

## 3. 消息传输路线详解

本节详细描述不同类型通信的完整消息路径，包括每一步的参与者、消息格式变化和关键处理逻辑。

### 3.1 服务调用（RPC）路线

#### 3.1.1 调用方（插件 A）发起请求
1. 插件 A 调用 `PluginMessaging.CallServiceAsync<TReq, TRes>("some.service", request)`。
2. `PluginMessaging` 内部构造一个**请求消息**（JSON），格式如下：
   ```json
   {
     "id": "550e8400-e29b-41d4-a716-446655440000",  // 自动生成的 UUID
     "from": "com.example.pluginA",                  // 插件 A 的 ID
     "to": "service:some.service",                   // 目标为服务名
     "action": "call",
     "payload": {
       "serviceName": "some.service",
       "request": { ... }                             // 开发者传入的请求对象
     },
     "version": "1.0"
   }
   ```
3. `PluginMessaging` 通过其持有的 `HostMessaging` 实例调用 `HostMessaging.SendToHostAsync`（底层调用主程序注入的发送委托），将消息字符串发送给主程序。

#### 3.1.2 主程序路由器处理
4. 主程序的 `MessageRouter` 收到消息字符串，反序列化为 `Message` 对象。
5. 路由器检查 `to` 字段，发现值为 `"service:some.service"`。
6. 路由器查询服务映射表 `_serviceMap`，获取提供 `"some.service"` 服务的插件 ID 列表。
   - 若列表为空，路由器构造错误响应（`SERVICE_NOT_FOUND`）并返回给插件 A。
   - 若列表非空，路由器根据策略选择一个提供者（当前策略：随机选择第一个；未来可支持轮询或指定）。假设选中插件 B。
7. 路由器修改消息对象：
   - 将 `to` 字段改为 `"plugin:com.example.pluginB"`。
   - 保持 `id`、`from`、`action`、`payload` 不变。
8. 路由器将修改后的消息重新序列化为 JSON，并从插件接收委托表中找到插件 B 的委托，调用 `receiver(messageJson)` 将消息转发给插件 B。

#### 3.1.3 被调用方（插件 B）接收并处理
9. 插件 B 的接收委托（即 `HostMessaging.HandleIncomingMessage`）被调用，传入消息 JSON。
10. `HostMessaging` 解析消息，发现 `action` 为 `"call"`，且 `to` 已变为 `"plugin:com.example.pluginB"`（但当前插件会忽略 `to`，因为已明确是发给自己的）。
11. `HostMessaging` 将消息通过其 `MessageReceived` 事件发布。`PluginMessaging` 订阅了此事件，并识别出这是服务调用请求（根据 `action` 和 `payload` 中的 `serviceName`）。
12. `PluginMessaging` 查找内部的服务处理器字典，找到注册的服务 `"some.service"` 对应的处理函数。
13. 调用处理函数，传入从 `payload.request` 反序列化得到的请求对象。
14. 处理函数执行完毕，返回响应对象。

#### 3.1.4 插件 B 发送响应
15. `PluginMessaging` 将响应对象包装成标准响应格式：
    ```json
    {
      "success": true,
      "data": { ... }   // 处理结果
    }
    ```
16. 构造**响应消息**：
    ```json
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",  // 与请求相同的 id
      "from": "com.example.pluginB",                  // 插件 B 的 ID
      "to": "host",                                    // 响应先发给主程序
      "action": "response",
      "payload": { "success": true, "data": { ... } },
      "version": "1.0"
    }
    ```
17. 插件 B 通过其 `HostMessaging` 将响应消息发送给主程序。

#### 3.1.5 主程序转发响应
18. 主程序收到响应消息，根据 `id` 在等待响应的字典中找到对应的 `TaskCompletionSource`（该字典在最初收到请求时由路由器记录）。
19. 路由器将响应消息原样（或必要时修改 `to` 为调用方插件 ID）转发给插件 A 的接收委托。

#### 3.1.6 调用方（插件 A）接收响应
20. 插件 A 的 `HostMessaging` 收到响应消息，根据 `id` 找到等待的 `TaskCompletionSource`，将响应数据传递给 `CallServiceAsync` 的调用者，最终方法返回结果。

### 3.2 直接消息（请求-响应）路线

直接消息的流程与服务调用类似，区别在于：
- 调用方使用 `SendToPluginAsync<TReq, TRes>(targetPluginId, action, request)`。
- 构造消息时，`to` 字段直接为 `"plugin:目标插件ID"`，`action` 为开发者自定义的字符串。
- 主程序路由器收到后，直接根据 `to` 查找目标插件接收委托并转发，无需查询服务映射。
- 目标插件通过 `PluginMessageReceived` 事件接收消息，并根据 `action` 自定义处理。
- 响应方式相同（通过 `HostMessaging` 发送带相同 `id` 的响应）。

### 3.3 直接消息（单向）路线

单向消息（`SendToPluginOneWayAsync`）不期待响应：
- 调用方构造消息，`id` 可选（因无响应），`to` 为目标插件。
- 主程序转发给目标插件后，**不维护任何等待字典**。
- 目标插件收到消息后触发事件，但无需发送响应。

### 3.4 广播路线

广播（`PublishToPluginsAsync`）：
- 调用方构造消息，`to` 字段为 `"*"`。
- 主程序路由器遍历所有插件接收委托，将消息副本发送给每个插件（**不修改 `from`，保留原始发送方**）。
- 所有插件收到消息后触发事件，可根据需要处理。
- 广播为单向，无响应。

---

## 4. 消息格式规范

所有插件间通信消息**必须**遵循以下 JSON 格式：

```json
{
  "id": "可选-UUID",
  "from": "发送方插件ID",
  "to": "接收方标识",
  "action": "操作名称",
  "payload": { ... },
  "timestamp": "ISO 8601 时间戳",
  "version": "1.0"
}
```

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `id` | string | 请求-响应时必需 | 消息唯一标识，应为 UUID 格式。用于将响应与请求关联。单向消息可省略。 |
| `from` | string | 是 | 发送方插件 ID。主程序会强制重写此字段为真实发送方，插件不可伪造。 |
| `to` | string | 是 | 接收方标识。取值：`"plugin:目标插件ID"`、`"service:服务名"`、`"*"`（广播）。 |
| `action` | string | 是 | 操作名称。服务调用时为 `"call"`，响应时为 `"response"`；直接消息时由开发者自定义。 |
| `payload` | object | 否 | 消息负载，可为任意 JSON 值。 |
| `timestamp` | string | 否 | ISO 8601 格式时间戳，如 `"2026-03-04T12:00:00Z"`。建议提供以便调试。 |
| `version` | string | 否 | 协议版本，默认为 `"1.0"`。 |

### 4.1 服务调用专用消息格式

当 `action` 为 `"call"` 时，`payload` **必须**包含以下结构：

```json
{
  "serviceName": "string",        // 要调用的服务名
  "request": { ... },              // 调用方传入的请求对象
  "targetPluginId": "string| null" // 可选，指定提供服务的插件ID
}
```

### 4.2 响应消息格式

当 `action` 为 `"response"` 时，`payload` **必须**采用统一包装：

```json
{
  "success": true,
  "data": { ... }                  // 成功时返回的数据
}
```

或

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "人类可读的错误描述"
  }
}
```

---

## 5. 服务注册与发现机制

### 5.1 服务注册
插件**必须**在初始化时（通常在 `PluginCommunication` 构造函数中）调用 `PluginMessaging.RegisterServiceAsync` 注册其提供的服务。

```csharp
await _pluginMessaging.RegisterServiceAsync<RequestType, ResponseType>(
    serviceName: "com.example.myservice",
    handler: async (req) => { ... },
    description: "服务的简短说明"
);
```

- **服务名**：应使用反向域名风格，确保全局唯一。例如：`"com.example.nationalspirit.parse"`。
- **处理函数**：必须为异步函数，接收请求对象，返回响应对象。通信库会自动处理 JSON 序列化。
- **描述**：建议提供，便于其他插件通过服务发现了解功能。

注册时，`PluginMessaging` 会通过 `HostMessaging` 向主程序发送一条 `registerService` 消息，主程序更新服务映射表。

### 5.2 服务发现
插件可通过 `GetServicesAsync()` 获取当前所有可用服务列表：

```csharp
var services = await _pluginMessaging.GetServicesAsync();
foreach (var svc in services)
{
    Console.WriteLine($"{svc.ServiceName} by {svc.ProviderPluginId}: {svc.Description}");
}
```

返回的 `ServiceInfo` 对象包含：
- `ServiceName`：服务名
- `ProviderPluginId`：提供服务的插件 ID
- `Description`：描述

### 5.3 服务注销
插件在禁用或卸载前**必须**注销自己注册的服务：

```csharp
await _pluginMessaging.UnregisterServiceAsync("com.example.myservice");
```

主程序会在插件卸载时自动清理其注册的服务，但显式注销仍是良好实践，可避免状态不一致。

---

## 6. API 参考

### 6.1 `PluginMessaging` 类

```csharp
public class PluginMessaging
{
    // 构造函数（插件必须在初始化时创建此实例）
    public PluginMessaging(HostMessaging hostMessaging, string pluginId);

    // 服务发现
    Task<List<ServiceInfo>> GetServicesAsync();

    // 服务调用
    Task<TResponse?> CallServiceAsync<TRequest, TResponse>(
        string serviceName,
        TRequest request,
        string? targetPluginId = null,
        CancellationToken cancellationToken = default);

    // 服务注册
    Task RegisterServiceAsync<TRequest, TResponse>(
        string serviceName,
        Func<TRequest, Task<TResponse>> handler,
        string? description = null);

    // 服务注销
    Task UnregisterServiceAsync(string serviceName);

    // 直接消息（请求-响应）
    Task<TResponse?> SendToPluginAsync<TRequest, TResponse>(
        string targetPluginId,
        string action,
        TRequest request,
        CancellationToken cancellationToken = default);

    // 直接消息（单向）
    Task SendToPluginOneWayAsync(string targetPluginId, string action, object payload);

    // 广播（单向）
    Task PublishToPluginsAsync(string action, object payload);

    // 事件：收到其他插件发来的消息（包括直接消息和广播）
    event EventHandler<PluginMessageEventArgs> PluginMessageReceived;

    // 简化订阅（基于 action）
    IDisposable Subscribe<T>(string action, Action<T> handler);
}
```

### 6.2 `ServiceInfo` 类

```csharp
public class ServiceInfo
{
    public string ServiceName { get; set; }
    public string ProviderPluginId { get; set; }
    public string? Description { get; set; }
}
```

---

## 7. 错误处理规范

### 7.1 标准错误码
当服务调用失败时，通信库会抛出异常，异常消息中包含以下预定义错误码之一：

| 错误码 | 说明 |
|--------|------|
| `SERVICE_NOT_FOUND` | 请求的服务不存在（没有插件注册该服务） |
| `SERVICE_PROVIDER_NOT_AVAILABLE` | 服务提供者插件已卸载或未响应 |
| `INVALID_REQUEST` | 请求参数格式错误（如缺少必要字段） |
| `HANDLER_EXCEPTION` | 服务处理函数抛出异常 |
| `TIMEOUT` | 调用超时（由 CancellationToken 触发） |
| `INTERNAL_ERROR` | 主程序或通信库内部错误 |

### 7.2 插件错误处理要求
- 调用 `CallServiceAsync` 或 `SendToPluginAsync` 时，必须使用 `try-catch` 捕获异常，并根据错误码执行适当的降级逻辑（如提示用户、重试、使用默认值）。
- 服务提供方的处理函数若预期内可能失败，应返回 `success: false` 并附带错误码，而非抛出异常。抛出异常会被通信库捕获并转换为 `HANDLER_EXCEPTION` 错误码。

### 7.3 超时处理
- 所有可能阻塞的调用应支持 `CancellationToken`。插件应合理设置超时（如 30 秒），并允许用户通过 UI 取消操作。

---

## 8. 最佳实践与注意事项

### 8.1 服务命名规范（强制）
- 服务名必须使用反向域名风格，例如 `com.yourcompany.yourplugin.servicename`。
- 禁止使用通用短名称（如 `"parser"`），以避免与其他插件冲突。

### 8.2 请求/响应类型定义（推荐）
- 由于不共享类型，调用方和被调用方需通过文档约定请求/响应对象的 JSON 结构。
- 建议在插件仓库中提供共享的 DTO 类库（仅包含数据类，无逻辑），方便其他插件引用。

### 8.3 版本兼容（推荐）
- 若服务需要升级，应注册新服务名（如 `com.example.service.v2`），旧服务继续保持兼容。
- 避免在同一个服务名上修改参数结构，否则会导致调用方不兼容。

### 8.4 资源清理（强制）
- 插件在收到 `pluginDisabling` 或 `pluginUnloading` 广播时，必须注销所有注册的服务。
- 必须移除所有事件订阅（`PluginMessageReceived` 或 `Subscribe` 返回的 `IDisposable`），防止内存泄漏。

### 8.5 性能考量
- 避免在服务处理函数中执行耗时操作（如文件读写、网络请求），应使用异步模式并支持取消。
- 广播事件会送达所有插件，处理函数应快速返回，避免影响整体性能。
- 大体积 payload（如数 MB）应避免在常规通信中使用；必要时采用分块或文件共享。

### 8.6 调试建议
- 在开发阶段，可临时启用消息日志：使用 `Subscribe` 订阅所有消息，打印到输出窗口。
- 利用主程序的 `log` 服务记录关键调用信息。

### 8.7 安全性
- 主程序会强制重写所有消息的 `from` 字段为真实发送方 ID，因此插件无需担心身份伪造。
- 若需限制服务调用者，可在服务处理函数中检查 `PluginMessageEventArgs.Message.From` 并实现白名单逻辑。

---

## 9. 版本兼容性声明

- 本规范对应的协议版本为 `1.0`。主程序在 `2.x` 系列中将保持协议向后兼容。
- 未来若有破坏性变更，将增加协议主版本号（如 `2.0`），并提前发布迁移指南。

---

## 10. 附录：完整示例

### 服务提供方插件（片段）

```csharp
public class PluginCommunication
{
    private readonly PluginMessaging _pluginMessaging;

    public PluginCommunication(HostMessaging hostMessaging)
    {
        _pluginMessaging = new PluginMessaging(hostMessaging, "com.example.provider");
        RegisterServices();
    }

    private async void RegisterServices()
    {
        await _pluginMessaging.RegisterServiceAsync<TextRequest, TextResult>(
            "com.example.text.reverse",
            async (req) =>
            {
                await Task.Delay(100);
                return new TextResult { Reversed = new string(req.Text.Reverse().ToArray()) };
            },
            "反转输入字符串"
        );
    }
}

public class TextRequest { public string Text { get; set; } }
public class TextResult { public string Reversed { get; set; } }
```

### 服务调用方插件（片段）

```csharp
public async Task CallReverseService(string input)
{
    try
    {
        var services = await _pluginMessaging.GetServicesAsync();
        var svc = services.FirstOrDefault(s => s.ServiceName == "com.example.text.reverse");
        if (svc != null)
        {
            var result = await _pluginMessaging.CallServiceAsync<TextRequest, TextResult>(
                svc.ServiceName,
                new TextRequest { Text = input },
                cancellationToken: cancellationTokenSource.Token
            );
            Console.WriteLine(result?.Reversed);
        }
    }
    catch (Exception ex)
    {
        // 处理错误
    }
}
```

---

## 11. 修订历史

| 版本 | 日期 | 变更说明 |
|------|------|----------|
| 1.0  | 2026-03-04 | 初始版本，基于新版通信系统制定强制规范 |

---

*文档版本：V1.0 简体中文版*<br>
<br>
**巴斯塔胡空间站**<br>
**2026-03-04**