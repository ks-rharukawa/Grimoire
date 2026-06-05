using System;
using System.Collections.Generic;

namespace Grimoire;

// 1 ラン中ずっと持続する状態 (戦闘をまたいで持ち越す)。
// classes.md 決定6: 1ラン = 5-7戦+ボス、デッキは戦闘後カード入手 (#9) で育つ。
public sealed class RunState
{
    public const int PlayerHpMaxBase = 30;

    public int PlayerHpMax { get; }
    public int PlayerHp { get; set; }       // 戦闘をまたいで持続 (回復は休憩ノード #10)
    public List<Card> Deck { get; }         // 所持カード (戦闘後3択 #9 で増える)
    public int BattlesWon { get; set; }
    public RunMap Map { get; }              // StS 風の分岐マップ (進行は Map が保持)

    public RunState()
    {
        PlayerHpMax = PlayerHpMaxBase;
        PlayerHp = PlayerHpMax;
        Deck = StarterDeck();
        Map = new RunMap(new Random());
    }

    // classes.md 決定5: 初期デッキ10枚 (Probe4 / Filter3 / Lookup2 / Echo1)
    public static List<Card> StarterDeck()
    {
        var deck = new List<Card>();
        for (int i = 0; i < 4; i++) deck.Add(new Card("Probe Request", 1, "過負荷源を\n診断し弱化", CardKind.Probe));
        for (int i = 0; i < 3; i++) deck.Add(new Card("Packet Filter", 1, "悪性流量を\n選別遮断", CardKind.Filter));
        for (int i = 0; i < 2; i++) deck.Add(new Card("Lookup",        1, "名前解決で\n+1 ドロー", CardKind.Lookup));
        deck.Add(new Card("Echo Reply", 2, "複製で\n複数対処", CardKind.Echo));
        return deck;
    }
}

// 戦闘後3択 (#9) の候補カードプール。現状は4 job の基本形 (deck 構成の選択)。
// per-card のパワー差/上位カードは将来のコンテンツ拡充 (classes.md カードプール設計、試作項目)。
public static class CardPool
{
    private static readonly Card[] All =
    {
        new("Probe Request", 1, "過負荷源を\n診断し弱化", CardKind.Probe),
        new("Packet Filter", 1, "悪性流量を\n選別遮断", CardKind.Filter),
        new("Lookup",        1, "名前解決で\n+1 ドロー", CardKind.Lookup),
        new("Echo Reply",    2, "複製で\n複数対処",     CardKind.Echo),
    };

    public static List<Card> Offer(Random rng, int n)
    {
        var pool = new List<Card>(All);
        var picks = new List<Card>();
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            int j = rng.Next(pool.Count);
            picks.Add(pool[j]);
            pool.RemoveAt(j);
        }
        return picks;
    }
}
