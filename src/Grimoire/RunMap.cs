using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimoire;

public enum MapNodeType { Battle, Elite, Rest, Boss }

public sealed class MapNode
{
    public MapNodeType Type;
    public int Layer;
    public int Col;                       // 0..2 (表示の横位置)
    public readonly List<int> NextIdx = new();   // 次層ノードリストへのインデックス
    public bool Visited;
}

// StS 風の分岐マップ。層状 DAG をランダム生成し、現在地から到達可能なノードを選んで上へ進む。
// classes.md 決定6: 5-7戦+ボス。生成は試作パラメータ。
public sealed class RunMap
{
    public const int NormalLayers = 6;    // 通常層 0..5、その上にボス層
    public const int Cols = 3;

    public List<List<MapNode>> Layers { get; } = new();
    public int BossLayer => NormalLayers;
    public int CurrentLayer { get; private set; } = -1;   // -1 = スタート (まだどこにも居ない)
    public int CurrentIdx { get; private set; } = -1;

    public RunMap(Random rng) => Generate(rng);

    private void Generate(Random rng)
    {
        for (int i = 0; i < NormalLayers; i++)
        {
            var layer = new List<MapNode>();
            foreach (var c in PickCols(rng))
                layer.Add(new MapNode { Layer = i, Col = c, Type = PickType(rng, i) });
            Layers.Add(layer);
        }
        Layers.Add(new List<MapNode> { new() { Layer = NormalLayers, Col = 1, Type = MapNodeType.Boss } });

        for (int i = 0; i < Layers.Count - 1; i++)
            BuildEdges(Layers[i], Layers[i + 1]);
    }

    private static List<int> PickCols(Random rng)
    {
        int n = 1 + rng.Next(Cols);       // 1..3 ノード
        var all = new List<int> { 0, 1, 2 };
        var chosen = new List<int>();
        for (int k = 0; k < n; k++)
        {
            int j = rng.Next(all.Count);
            chosen.Add(all[j]);
            all.RemoveAt(j);
        }
        chosen.Sort();
        return chosen;
    }

    private static MapNodeType PickType(Random rng, int layer)
    {
        if (layer == NormalLayers - 1) return MapNodeType.Rest;   // ボス直前は休憩
        double r = rng.NextDouble();
        if (r < 0.18) return MapNodeType.Elite;
        if (r < 0.30) return MapNodeType.Rest;
        return MapNodeType.Battle;
    }

    // col 隣接でつなぎ、到達性 (出口なし/入口なし) を補正してパスを保証する。
    private static void BuildEdges(List<MapNode> from, List<MapNode> to)
    {
        foreach (var a in from)
            for (int j = 0; j < to.Count; j++)
                if (Math.Abs(to[j].Col - a.Col) <= 1) a.NextIdx.Add(j);

        foreach (var a in from)
            if (a.NextIdx.Count == 0) a.NextIdx.Add(NearestIdx(to, a.Col));

        for (int j = 0; j < to.Count; j++)
            if (!from.Any(a => a.NextIdx.Contains(j)))
                NearestNode(from, to[j].Col).NextIdx.Add(j);
    }

    private static int NearestIdx(List<MapNode> nodes, int col)
    {
        int best = 0, bestd = int.MaxValue;
        for (int j = 0; j < nodes.Count; j++)
        {
            int d = Math.Abs(nodes[j].Col - col);
            if (d < bestd) { bestd = d; best = j; }
        }
        return best;
    }

    private static MapNode NearestNode(List<MapNode> nodes, int col) => nodes[NearestIdx(nodes, col)];

    // 現在地から到達可能な次ノード一覧
    public List<MapNode> ReachableNext()
    {
        if (CurrentLayer < 0) return new List<MapNode>(Layers[0]);          // スタート → 層0 の全ノード
        if (CurrentLayer >= Layers.Count - 1) return new List<MapNode>();   // ボス到達済み
        var cur = Layers[CurrentLayer][CurrentIdx];
        return cur.NextIdx.Select(j => Layers[CurrentLayer + 1][j]).ToList();
    }

    public bool IsReachable(MapNode node) => ReachableNext().Contains(node);

    public void Advance(MapNode node)
    {
        int idx = Layers[node.Layer].IndexOf(node);
        if (idx < 0) return;
        CurrentLayer = node.Layer;
        CurrentIdx = idx;
        node.Visited = true;
    }

    public MapNode? Current => CurrentLayer >= 0 ? Layers[CurrentLayer][CurrentIdx] : null;
}
