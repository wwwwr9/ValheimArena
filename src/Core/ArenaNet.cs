using System;
using System.Collections.Generic;

namespace ValheimArena.Core
{
    public static class ArenaNet
    {
        public const int Protocol = 7;

        private const string RState = "VA_State";
        private const string RHello = "VA_Hello";
        private const string RCmd = "VA_Cmd";
        private const string RClear = "VA_Clear";
        private const string RDmg = "VA_Dmg";
        private const string RKill = "VA_Kill";
        private const string RStats = "VA_Stats";

        private static ZRoutedRpc _registered;
        private static bool _helloSent;
        private static bool _warnedProtocol;

        public static bool Ready => ZRoutedRpc.instance != null && _registered == ZRoutedRpc.instance;

        public static void Update()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                _registered = null;
                return;
            }
            if (rpc != _registered)
            {
                try
                {
                    rpc.Register<ZPackage>(RState, OnState);
                    rpc.Register<ZPackage>(RHello, OnHello);
                    rpc.Register<ZPackage>(RCmd, OnCmd);
                    rpc.Register<ZPackage>(RClear, OnClear);
                    rpc.Register<ZPackage>(RDmg, OnDmg);
                    rpc.Register<ZPackage>(RKill, OnKill);
                    rpc.Register<ZPackage>(RStats, OnStats);
                }
                catch (Exception e)
                {
                    ArenaPlugin.Log.LogWarning(L.T("Сетевые вызовы арены уже зарегистрированы: ") + e.Message);
                }
                _registered = rpc;
                _helloSent = false;
            }
            if (!_helloSent && Player.m_localPlayer)
            {
                _helloSent = true;
                Send(ZRoutedRpc.Everybody, RHello, new ZPackage());
            }
        }

        private static void Send(long target, string method, ZPackage pkg)
        {
            if (!Ready) return;
            try { ZRoutedRpc.instance.InvokeRoutedRPC(target, method, pkg); }
            catch (Exception e) { ArenaPlugin.Log.LogWarning(L.F("Отправка {0} не удалась: {1}", method, e.Message)); }
        }

        private static ZPackage Json(object o)
        {
            var pkg = new ZPackage();
            pkg.Write(JsonIO.ToJson(o));
            return pkg;
        }

        private static T ReadJson<T>(ZPackage pkg) where T : class
        {
            try { return JsonIO.FromJson<T>(pkg.ReadString()); }
            catch (Exception e)
            {
                ArenaPlugin.Log.LogWarning(L.F("Сообщение арены не разобрано ({0}): {1}", typeof(T).Name, e.Message));
                return null;
            }
        }

        public static void SendState(ArenaSession s) => Send(ZRoutedRpc.Everybody, RState, Json(s.ToState()));

        public static void SendCommand(ArenaSession s, string cmd, string who)
        {
            var pkg = new ZPackage();
            pkg.Write(s.Id);
            pkg.Write(cmd);
            pkg.Write(who ?? "");
            Send(s.HostPeer, RCmd, pkg);
        }

        public static void SendClear(string sessionId, ClearWhat what)
        {
            var pkg = new ZPackage();
            pkg.Write(sessionId);
            pkg.Write((int)what);
            Send(ZRoutedRpc.Everybody, RClear, pkg);
        }

        public static void SendDamage(ArenaSession s, Dictionary<string, float> dealt, Dictionary<string, float> taken)
        {
            var pkg = new ZPackage();
            pkg.Write(s.Id);
            pkg.Write(dealt.Count);
            foreach (var kv in dealt) { pkg.Write(kv.Key); pkg.Write(kv.Value); }
            pkg.Write(taken.Count);
            foreach (var kv in taken) { pkg.Write(kv.Key); pkg.Write(kv.Value); }
            Send(s.HostPeer, RDmg, pkg);
        }

        public static void SendKill(KillEvent e) => Send(ZRoutedRpc.Everybody, RKill, Json(e));

        public static void SendStats(ArenaSession s, bool full)
        {
            var sum = new StatsSummary { session = s.Id, full = full };
            if (full)
            {
                foreach (var st in s.Stats.Values)
                    if (!string.IsNullOrEmpty(st.label)) sum.units.Add(st);
            }
            else
            {
                foreach (var key in s.DirtyStats)
                    if (s.Stats.TryGetValue(key, out var st) && !string.IsNullOrEmpty(st.label)) sum.units.Add(st);
            }
            s.DirtyStats.Clear();
            if (full) s.StatsFull = false;
            if (!full && sum.units.Count == 0) return;
            Send(ZRoutedRpc.Everybody, RStats, Json(sum));
        }

        private static void OnState(long sender, ZPackage pkg)
        {
            var st = ReadJson<SessionState>(pkg);
            if (st == null) return;
            if (st.protocol != Protocol)
            {
                Arena.NoteForeignSession(st.id);
                if (!_warnedProtocol)
                {
                    _warnedProtocol = true;
                    Arena.Message(L.F("Арена игрока {0}: другая версия мода (протокол {1}, у вас {2}) — обновитесь до одной версии.", st.hostName, st.protocol, Protocol));
                }
                return;
            }
            Arena.OnRemoteState(st);
        }

        private static void OnHello(long sender, ZPackage pkg) => Arena.OnHello();

        private static void OnCmd(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString();
            string cmd = pkg.ReadString();
            string who = pkg.ReadString();
            if (Arena.Local != null && Arena.Local.Id == id) Arena.ExecuteHostCommand(cmd, who, fromGuest: true);
        }

        private static void OnClear(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString();
            var what = (ClearWhat)pkg.ReadInt();
            Arena.OnClear(id, what);
        }

        private static void OnDmg(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString();
            var dealt = new Dictionary<string, float>();
            var taken = new Dictionary<string, float>();
            int n = pkg.ReadInt();
            for (int i = 0; i < n; i++) { string k = pkg.ReadString(); dealt[k] = pkg.ReadSingle(); }
            n = pkg.ReadInt();
            for (int i = 0; i < n; i++) { string k = pkg.ReadString(); taken[k] = pkg.ReadSingle(); }
            Arena.OnDamageReport(id, dealt, taken);
        }

        private static void OnKill(long sender, ZPackage pkg)
        {
            var e = ReadJson<KillEvent>(pkg);
            if (e != null) Arena.OnKill(e);
        }

        private static void OnStats(long sender, ZPackage pkg)
        {
            var sum = ReadJson<StatsSummary>(pkg);
            if (sum != null) Arena.OnStatsSummary(sum);
        }
    }
}
