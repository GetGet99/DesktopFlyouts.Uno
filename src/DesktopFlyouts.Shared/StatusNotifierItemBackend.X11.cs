#if HAS_UNO
// StatusNotifierItem D-Bus backend for KDE Plasma.
// Exposes an object on the session bus that Plasma calls into (Activate, ContextMenu, etc.)
// Registers with org.kde.StatusNotifierWatcher to become visible in the system tray.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using DesktopFlyouts.DBus;

namespace DesktopFlyouts
{
    internal class StatusNotifierItemBackend : IDisposable
    {
        private const string WatcherServiceName = "org.kde.StatusNotifierWatcher";
        private const string WatcherObjectPath = "/StatusNotifierWatcher";
        private const string ItemObjectPath = "/StatusNotifierItem";

        private string _busName;
        private readonly string _id;
        private readonly string _iconName;
        private string _tooltip;
        private readonly object _lock = new();

        private DBusConnection? _connection;
        private StatusNotifierItemHandler? _handler;
        private bool _disposed;
        private bool _registered;
        private string _status = "Active";
        private string _currentIconName;
        private string _currentIconThemePath = string.Empty;

        internal event EventHandler<MouseEventReceivedEventArgs>? LeftClicked;
        internal event EventHandler<MouseEventReceivedEventArgs>? RightClicked;
        internal event EventHandler<MouseEventReceivedEventArgs>? LeftDoubleClicked;
        internal event EventHandler<MouseEventReceivedEventArgs>? RightDoubleClicked;

        internal StatusNotifierItemBackend(string iconPath, string tooltip, Guid id)
        {
            _id = $"DesktopFlyouts-{Environment.ProcessId}-{id:N}";
            _busName = $"org.kde.StatusNotifierItem-{Environment.ProcessId}-{id:N}";
            _tooltip = tooltip;

            var (iconName, themePath) = ResolveIcon(iconPath);
            _iconName = iconName;
            _currentIconName = iconName;
            _currentIconThemePath = themePath;
        }

        internal void Show()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (_registered)
                {
                    _status = "Active";
                    _handler?.UpdateStatus("Active");
                    return;
                }
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("[StatusNotifierItem] Starting D-Bus setup...");
                    var sessionAddress = DBusAddress.Session;
                    if (sessionAddress is null)
                    {
                        Debug.WriteLine("[StatusNotifierItem] No D-Bus session address found.");
                        return;
                    }
                    Debug.WriteLine($"[StatusNotifierItem] Session address: {sessionAddress}");

                    _connection = new DBusConnection(sessionAddress);
                    await _connection.ConnectAsync();
                    Debug.WriteLine("[StatusNotifierItem] Connected to D-Bus.");

                    Debug.WriteLine($"[StatusNotifierItem] Requesting bus name: {_busName}");
                    bool acquired = await _connection.TryRequestNameAsync(_busName);
                    Debug.WriteLine($"[StatusNotifierItem] Name acquired: {acquired}");
                    if (!acquired)
                    {
                        var altBusName = $"{_busName}-{Guid.NewGuid():N}";
                        acquired = await _connection.TryRequestNameAsync(altBusName);
                        Debug.WriteLine($"[StatusNotifierItem] Alt name acquired: {acquired} ({altBusName})");
                        if (!acquired)
                            return;
                        _busName = altBusName;
                    }

                    _handler = new StatusNotifierItemHandler(_connection, ItemObjectPath);
                    _handler.Initialize(_id, _tooltip, _status, _currentIconName, _currentIconThemePath);
                    _handler.LeftClicked += OnActivate;
                    _handler.RightClicked += OnContextMenu;
                    _handler.LeftDoubleClicked += OnSecondaryActivate;
                    _handler.RightDoubleClicked += OnRightDoubleClicked;
                    _connection.AddMethodHandler(_handler);
                    Debug.WriteLine("[StatusNotifierItem] Method handler registered at /StatusNotifierItem.");

                    var watcherService = new DBusService(_connection, WatcherServiceName);
                    var watcher = watcherService.CreateStatusNotifierWatcher(new ObjectPath(WatcherObjectPath));
                    Debug.WriteLine("[StatusNotifierItem] Registering with watcher as object path.");
                    await watcher.RegisterStatusNotifierItemAsync(ItemObjectPath);
                    Debug.WriteLine("[StatusNotifierItem] Registered with StatusNotifierWatcher successfully.");

                    lock (_lock)
                    {
                        _registered = true;
                        _status = "Active";
                    }

                    _handler.UpdateStatus("Active");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StatusNotifierItem] D-Bus setup failed: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException is not null)
                        Debug.WriteLine($"[StatusNotifierItem]   Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            });
        }

        internal void Destroy()
        {
            lock (_lock)
            {
                _registered = false;
            }

            if (_handler is not null)
            {
                _handler.LeftClicked -= OnActivate;
                _handler.RightClicked -= OnContextMenu;
                _handler.LeftDoubleClicked -= OnSecondaryActivate;
                _handler.RightDoubleClicked -= OnRightDoubleClicked;
            }

            if (_connection is not null)
            {
                _connection.RemoveMethodHandler(ItemObjectPath);
                _connection.Dispose();
            }

            _connection = null;
            _handler = null;
        }

        internal void SetIcon(string iconPath)
        {
            var (iconName, themePath) = ResolveIcon(iconPath);
            lock (_lock)
            {
                _currentIconName = iconName;
                _currentIconThemePath = themePath;
            }

            _handler?.UpdateIcon(_currentIconName, _currentIconThemePath);
        }

        internal void SetStatus(string status)
        {
            _status = status;
            _handler?.UpdateStatus(status);
        }

        internal void SetTooltip(string tooltip)
        {
            _tooltip = tooltip;
            _handler?.UpdateTooltip(tooltip);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Destroy();
            GC.SuppressFinalize(this);
        }

        private void OnActivate(int x, int y)
        {
            LeftClicked?.Invoke(this, new MouseEventReceivedEventArgs(new Point(x, y)));
        }

        private void OnContextMenu(int x, int y)
        {
            RightClicked?.Invoke(this, new MouseEventReceivedEventArgs(new Point(x, y)));
        }

        private void OnSecondaryActivate(int x, int y)
        {
            LeftDoubleClicked?.Invoke(this, new MouseEventReceivedEventArgs(new Point(x, y)));
        }

        private void OnRightDoubleClicked(int x, int y)
        {
            RightDoubleClicked?.Invoke(this, new MouseEventReceivedEventArgs(new Point(x, y)));
        }

        private static (string iconName, string themePath) ResolveIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return ("dialog-information", string.Empty);

            if (File.Exists(iconPath))
            {
                var fullPath = Path.GetFullPath(iconPath);
                var fileName = Path.GetFileNameWithoutExtension(fullPath);
                var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;

                if (directory.Contains("/icons/") || directory.Contains("/hicolor/"))
                {
                    return (fileName, directory);
                }

                return (fullPath, string.Empty);
            }

            return (iconPath, string.Empty);
        }

        /// <summary>
        /// IPathMethodHandler that Plasma calls into for Activate, ContextMenu, etc.
        /// Also handles org.freedesktop.DBus.Properties queries for our properties.
        /// </summary>
        private class StatusNotifierItemHandler : IPathMethodHandler, IDisposable
        {
            private const string InterfaceName = "org.kde.StatusNotifierItem";
            private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
            private const string IntrospectableInterface = "org.freedesktop.DBus.Introspectable";

            private static readonly ReadOnlyMemory<byte> IntrospectableXml = System.Text.Encoding.UTF8.GetBytes("""
                <interface name="org.freedesktop.DBus.Introspectable">
                    <method name="Introspect">
                        <arg name="data" type="s" direction="out"/>
                    </method>
                </interface>
                """);

            private static readonly ReadOnlyMemory<byte> PropertiesXml = System.Text.Encoding.UTF8.GetBytes("""
                <interface name="org.freedesktop.DBus.Properties">
                    <method name="Get">
                        <arg name="interface_name" type="s" direction="in"/>
                        <arg name="property_name" type="s" direction="in"/>
                        <arg name="value" type="v" direction="out"/>
                    </method>
                    <method name="Set">
                        <arg name="interface_name" type="s" direction="in"/>
                        <arg name="property_name" type="s" direction="in"/>
                        <arg name="value" type="v" direction="in"/>
                    </method>
                    <method name="GetAll">
                        <arg name="interface_name" type="s" direction="in"/>
                        <arg name="properties" type="a{sv}" direction="out"/>
                    </method>
                    <signal name="PropertiesChanged">
                        <arg name="interface_name" type="s"/>
                        <arg name="changed_properties" type="a{sv}"/>
                        <arg name="invalidated_properties" type="as"/>
                    </signal>
                </interface>
                """);

            private static readonly ReadOnlyMemory<byte> StatusNotifierXml = System.Text.Encoding.UTF8.GetBytes("""
                <interface name="org.kde.StatusNotifierItem">
                    <method name="Activate">
                        <arg name="x" type="i" direction="in"/>
                        <arg name="y" type="i" direction="in"/>
                    </method>
                    <method name="SecondaryActivate">
                        <arg name="x" type="i" direction="in"/>
                        <arg name="y" type="i" direction="in"/>
                    </method>
                    <method name="ContextMenu">
                        <arg name="x" type="i" direction="in"/>
                        <arg name="y" type="i" direction="in"/>
                    </method>
                    <method name="Scroll">
                        <arg name="delta" type="i" direction="in"/>
                        <arg name="orientation" type="s" direction="in"/>
                    </method>
                    <property name="Category" type="s" access="read"/>
                    <property name="Id" type="s" access="read"/>
                    <property name="Title" type="s" access="read"/>
                    <property name="Status" type="s" access="read"/>
                    <property name="WindowId" type="i" access="read"/>
                    <property name="IconName" type="s" access="read"/>
                    <property name="IconPixmap" type="a(iiay)" access="read"/>
                    <property name="OverlayIconName" type="s" access="read"/>
                    <property name="OverlayIconPixmap" type="a(iiay)" access="read"/>
                    <property name="AttentionIconName" type="s" access="read"/>
                    <property name="AttentionIconPixmap" type="a(iiay)" access="read"/>
                    <property name="AttentionMovieName" type="s" access="read"/>
                    <property name="ToolTip" type="(sa(iiay)ss)" access="read"/>
                    <property name="IconThemePath" type="s" access="read"/>
                    <property name="Menu" type="o" access="read"/>
                    <property name="ItemIsMenu" type="b" access="read"/>
                    <signal name="NewTitle"/>
                    <signal name="NewIcon"/>
                    <signal name="NewAttentionIcon"/>
                    <signal name="NewOverlayIcon"/>
                    <signal name="NewToolTip"/>
                    <signal name="NewStatus">
                        <arg name="status" type="s"/>
                    </signal>
                    <signal name="NewIconThemePath">
                        <arg name="icon_theme_path" type="s"/>
                    </signal>
                </interface>
                """);

            private readonly DBusConnection _connection;
            private string _id = string.Empty;
            private string _title = string.Empty;
            private string _status = "Active";
            private string _iconName = string.Empty;
            private string _iconThemePath = string.Empty;

            public event Action<int, int>? LeftClicked;
            public event Action<int, int>? RightClicked;
            public event Action<int, int>? LeftDoubleClicked;
            public event Action<int, int>? RightDoubleClicked;

            public string Path { get; }
            public bool HandlesChildPaths => false;

            public StatusNotifierItemHandler(DBusConnection connection, string path)
            {
                _connection = connection;
                Path = path;
            }

            public void Initialize(string id, string title, string status, string iconName, string iconThemePath)
            {
                _id = id;
                _title = title;
                _status = status;
                _iconName = iconName;
                _iconThemePath = iconThemePath;
            }

            public void UpdateIcon(string iconName, string iconThemePath)
            {
                _iconName = iconName;
                _iconThemePath = iconThemePath;
                EmitPropertiesChanged();
                EmitNewIcon();
                EmitNewIconThemePath(_iconThemePath);
            }

            public void UpdateStatus(string status)
            {
                _status = status;
                EmitPropertiesChanged();
                EmitNewStatus(status);
            }

            public void UpdateTooltip(string tooltip)
            {
                _title = tooltip;
                EmitPropertiesChanged();
                EmitNewToolTip();
            }

            public ValueTask HandleMethodAsync(MethodContext context)
            {
                if (context.IsDBusIntrospectRequest)
                {
                    context.ReplyIntrospectXml([IntrospectableXml, PropertiesXml, StatusNotifierXml]);
                    return default;
                }

                var request = context.Request;

                if (request.InterfaceAsString == PropertiesInterface)
                {
                    return HandlePropertiesAsync(context);
                }

                if (request.InterfaceAsString == IntrospectableInterface)
                {
                    context.ReplyIntrospectXml([IntrospectableXml, PropertiesXml, StatusNotifierXml]);
                    return default;
                }

                if (request.InterfaceAsString == InterfaceName)
                {
                    return request.MemberAsString switch
                    {
                        "Activate" => HandleActivate(context),
                        "ContextMenu" => HandleContextMenu(context),
                        "SecondaryActivate" => HandleSecondaryActivate(context),
                        "Scroll" => HandleScroll(context),
                        _ => ReplyEmpty(context),
                    };
                }

                return ReplyEmpty(context);
            }

            private static ValueTask ReplyEmpty(MethodContext context)
            {
                using var writer = context.CreateReplyWriter("");
                context.Reply(writer.CreateMessage());
                return default;
            }

            private ValueTask HandleActivate(MethodContext context)
            {
                var reader = context.Request.GetBodyReader();
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                LeftClicked?.Invoke(x, y);
                return ReplyEmpty(context);
            }

            private ValueTask HandleContextMenu(MethodContext context)
            {
                var reader = context.Request.GetBodyReader();
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                RightClicked?.Invoke(x, y);
                return ReplyEmpty(context);
            }

            private ValueTask HandleSecondaryActivate(MethodContext context)
            {
                var reader = context.Request.GetBodyReader();
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                LeftDoubleClicked?.Invoke(x, y);
                return ReplyEmpty(context);
            }

            private ValueTask HandleScroll(MethodContext context)
            {
                var reader = context.Request.GetBodyReader();
                int delta = reader.ReadInt32();
                string orientation = reader.ReadString();
                return ReplyEmpty(context);
            }

            private ValueTask HandlePropertiesAsync(MethodContext context)
            {
                var member = context.Request.MemberAsString;
                return member switch
                {
                    "Get" => HandlePropertyGetAsync(context),
                    "GetAll" => HandlePropertyGetAllAsync(context),
                    "Set" => HandlePropertySetAsync(context),
                    _ => ReplyEmpty(context),
                };
            }

            private ValueTask HandlePropertyGetAsync(MethodContext context)
            {
                var reader = context.Request.GetBodyReader();
                reader.ReadString(); // interface name
                var propertyName = reader.ReadString();

                using var writer = context.CreateReplyWriter("v");
                WritePropertyValue(writer, propertyName);
                context.Reply(writer.CreateMessage());
                return default;
            }

            private ValueTask HandlePropertyGetAllAsync(MethodContext context)
            {
                var reader = context.Request.GetBodyReader();
                reader.ReadString(); // interface name - consume but ignore

                using var writer = context.CreateReplyWriter("a{sv}");
                var dictStart = writer.WriteArrayStart(DBusType.DictEntry);

                writer.WriteStructureStart();
                writer.WriteString("Category");
                writer.WriteVariantString("ApplicationStatus");

                writer.WriteStructureStart();
                writer.WriteString("Id");
                writer.WriteVariantString(_id);

                writer.WriteStructureStart();
                writer.WriteString("Title");
                writer.WriteVariantString(_title);

                writer.WriteStructureStart();
                writer.WriteString("Status");
                writer.WriteVariantString(_status);

                writer.WriteStructureStart();
                writer.WriteString("WindowId");
                writer.WriteVariantInt32(0);

                writer.WriteStructureStart();
                writer.WriteString("IconName");
                writer.WriteVariantString(_iconName);

                writer.WriteStructureStart();
                writer.WriteString("IconThemePath");
                writer.WriteVariantString(_iconThemePath);

                writer.WriteStructureStart();
                writer.WriteString("ItemIsMenu");
                writer.WriteVariantBool(false);

                writer.WriteStructureStart();
                writer.WriteString("Menu");
                writer.WriteVariantObjectPath("/");

                writer.WriteStructureStart();
                writer.WriteString("OverlayIconName");
                writer.WriteVariantString(string.Empty);

                writer.WriteStructureStart();
                writer.WriteString("AttentionIconName");
                writer.WriteVariantString(string.Empty);

                writer.WriteStructureStart();
                writer.WriteString("AttentionMovieName");
                writer.WriteVariantString(string.Empty);

                writer.WriteArrayEnd(dictStart);
                context.Reply(writer.CreateMessage());
                return default;
            }

            private ValueTask HandlePropertySetAsync(MethodContext context)
            {
                return ReplyEmpty(context);
            }

            private void WritePropertyValue(MessageWriter writer, string propertyName)
            {
                switch (propertyName)
                {
                    case "ItemIsMenu":
                        writer.WriteSignature("b");
                        writer.WriteBool(false);
                        break;
                    case "Category":
                        writer.WriteSignature("s");
                        writer.WriteString("ApplicationStatus");
                        break;
                    case "Id":
                        writer.WriteSignature("s");
                        writer.WriteString(_id);
                        break;
                    case "Title":
                        writer.WriteSignature("s");
                        writer.WriteString(_title);
                        break;
                    case "Status":
                        writer.WriteSignature("s");
                        writer.WriteString(_status);
                        break;
                    case "WindowId":
                        writer.WriteSignature("i");
                        writer.WriteInt32(0);
                        break;
                    case "IconName":
                        writer.WriteSignature("s");
                        writer.WriteString(_iconName);
                        break;
                    case "IconThemePath":
                        writer.WriteSignature("s");
                        writer.WriteString(_iconThemePath);
                        break;
                    case "IconPixmap":
                        writer.WriteSignature("a(iiay)");
                        var pixStart = writer.WriteArrayStart(DBusType.Struct);
                        writer.WriteArrayEnd(pixStart);
                        break;
                    case "OverlayIconName":
                        writer.WriteSignature("s");
                        writer.WriteString(string.Empty);
                        break;
                    case "OverlayIconPixmap":
                        writer.WriteSignature("a(iiay)");
                        var overlayPixStart = writer.WriteArrayStart(DBusType.Struct);
                        writer.WriteArrayEnd(overlayPixStart);
                        break;
                    case "AttentionIconName":
                        writer.WriteSignature("s");
                        writer.WriteString(string.Empty);
                        break;
                    case "AttentionIconPixmap":
                        writer.WriteSignature("a(iiay)");
                        var attentionPixStart = writer.WriteArrayStart(DBusType.Struct);
                        writer.WriteArrayEnd(attentionPixStart);
                        break;
                    case "AttentionMovieName":
                        writer.WriteSignature("s");
                        writer.WriteString(string.Empty);
                        break;
                    case "ToolTip":
                        writer.WriteSignature("(sa(iiay)ss)");
                        writer.WriteStructureStart();
                        writer.WriteString(_title);
                        var tipArrayStart = writer.WriteArrayStart(DBusType.Struct);
                        writer.WriteArrayEnd(tipArrayStart);
                        writer.WriteString(string.Empty);
                        writer.WriteString(string.Empty);
                        break;
                    case "Menu":
                        writer.WriteSignature("o");
                        writer.WriteString("/");
                        break;
                    default:
                        writer.WriteSignature("s");
                        writer.WriteString(string.Empty);
                        break;
                }
            }

            private void EmitPropertiesChanged()
            {
                if (_connection is null)
                    return;

                using var writer = _connection.GetMessageWriter();
                writer.WriteSignalHeader(
                    destination: "",
                    path: Path,
                    @interface: PropertiesInterface,
                    member: "PropertiesChanged",
                    signature: "sa{sv}as");

                writer.WriteString(InterfaceName);

                var dictStart = writer.WriteArrayStart(DBusType.DictEntry);

                writer.WriteStructureStart();
                writer.WriteString("Status");
                writer.WriteVariantString(_status);

                writer.WriteStructureStart();
                writer.WriteString("IconName");
                writer.WriteVariantString(_iconName);

                writer.WriteStructureStart();
                writer.WriteString("IconThemePath");
                writer.WriteVariantString(_iconThemePath);

                writer.WriteArrayEnd(dictStart);

                var arrayStart = writer.WriteArrayStart(DBusType.String);
                writer.WriteArrayEnd(arrayStart);

                _connection.TrySendMessage(writer.CreateMessage());
            }

            private void EmitNewIcon()
            {
                if (_connection is null)
                    return;

                using var writer = _connection.GetMessageWriter();
                writer.WriteSignalHeader(
                    destination: "",
                    path: Path,
                    @interface: InterfaceName,
                    member: "NewIcon",
                    signature: "");
                _connection.TrySendMessage(writer.CreateMessage());
            }

            private void EmitNewToolTip()
            {
                if (_connection is null)
                    return;

                using var writer = _connection.GetMessageWriter();
                writer.WriteSignalHeader(
                    destination: "",
                    path: Path,
                    @interface: InterfaceName,
                    member: "NewToolTip",
                    signature: "");
                _connection.TrySendMessage(writer.CreateMessage());
            }

            private void EmitNewStatus(string status)
            {
                if (_connection is null)
                    return;

                using var writer = _connection.GetMessageWriter();
                writer.WriteSignalHeader(
                    destination: "",
                    path: Path,
                    @interface: InterfaceName,
                    member: "NewStatus",
                    signature: "s");
                writer.WriteString(status);
                _connection.TrySendMessage(writer.CreateMessage());
            }

            private void EmitNewIconThemePath(string iconThemePath)
            {
                if (_connection is null)
                    return;

                using var writer = _connection.GetMessageWriter();
                writer.WriteSignalHeader(
                    destination: "",
                    path: Path,
                    @interface: InterfaceName,
                    member: "NewIconThemePath",
                    signature: "s");
                writer.WriteString(iconThemePath);
                _connection.TrySendMessage(writer.CreateMessage());
            }

            public void Dispose()
            {
            }
        }
    }
}
#endif
