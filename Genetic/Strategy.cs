using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Genetic
{
    public struct Edge
    {
        public Int32 Id, NodeBId, NodeAId, Length;
        public Int32 GetAnother(int id) => NodeAId == id ? NodeBId : NodeAId;
        public bool HaveNode(int id) => NodeBId == id || NodeAId == id;
        public bool HaveNodes(int idA, int idB) => NodeAId == idA && NodeBId == idB || NodeBId == idA && NodeAId == idB;
        public override string ToString() => NodeAId + "-" + NodeBId;
    }
    public struct Node
    {
        public Int32 Id;
        public Int32[] EdgesId;
        public Int32[] NodesId;
    }
    public struct Dna
    {
        public List<Int32> Genom;
        public static Dna New(Node[] nodes, Int32 start, Random rand, object randLock)
        {
            Dna res = new Dna();
            Int32 pathLen;
            lock (randLock) pathLen = rand.Next(nodes.Length - 1, nodes.Length + (nodes.Length >> 1));
            Int32 nodeIndex = start;
            Int32 lastNode;
            res.Genom = new List<Int32>(pathLen);
            for (int i = 0; i < pathLen; i++)
            {
                lastNode = nodeIndex;
                res.Genom.Add(nodeIndex);
                lock (randLock) nodeIndex = nodes[nodeIndex].NodesId[rand.Next(0, nodes[nodeIndex].NodesId.Length)];
            }
            return res;
        }
        public void Mutate(Int32 ver, Random rand, object randLock, Node[] nodes)
        {
            Int32 count = Genom.Count;
            Int32 len = nodes.Length;
            bool mutate;
            Int32 index;
            for (int i = 1; i < count; i++)
            {
                lock (randLock) mutate = rand.Next(0, 100) < ver;
                if (!mutate) continue;

                lock (randLock) index = nodes[Genom[i]].NodesId[rand.Next(0, nodes[Genom[i]].NodesId.Length)];
                Genom[i] = nodes[index].Id;
            }
            lock (randLock)
            {
                mutate = rand.Next(0, 100) < ver;
            }
            if (mutate)
            {
                lock (randLock) mutate = ((rand.Next() << 1) & 1) == 0;
                if (mutate)
                {
                    index = Genom.Count - 1;
                    lock (randLock) index = nodes[Genom[index]].NodesId[rand.Next(0, nodes[Genom[index]].NodesId.Length)];
                    Genom.Add(nodes[index].Id);
                }
                else
                {
                    lock (randLock) index = rand.Next(1, count);
                    Genom.RemoveAt(index);
                }
            }
            for (int i = 1; i < count; i++)
            {
                if (Genom[i] == Genom[i - 1]) Genom[i] = (Genom[i] + 1) % len;
            }
        }
        public static Dna Cross(Dna a, Dna b)
        {
            Dna res = new Dna();
            Int32 aCount = a.Genom.Count;
            Int32 bCount = a.Genom.Count;
            res.Genom = new List<Int32>(aCount);
            if (aCount <= bCount)
            {
                for (int i = 0; i < aCount; i++)
                    res.Genom.Add((i << 1 & 1) == 0 ? a.Genom[i] : b.Genom[i]);
            }
            else
            {
                Int32 point = bCount;
                for (int i = 0; i < aCount; i++)
                    if (i < point)
                        res.Genom.Add((i << 1 & 1) == 0 ? a.Genom[i] : b.Genom[i]);
                    else
                        res.Genom.Add(a.Genom[i]);
            }
            return res;
        }
        public Int32 Length(Edge[] edges)
        {
            Int32 res = 0;
            Int32 count = Genom.Count - 1;
            Int32 idA;
            Int32 idB;
            for (int i = 0; i < count; i++)
            {
                idA = Genom[i];
                idB = Genom[i + 1];
                res += edges.Where(x => x.HaveNodes(idA, idB)).FirstOrDefault().Length;
            }
            return res;
        }
        public bool Validate(Edge[] edges, Node[] nodes)
        {
            Int32 count = Genom.Count - 1;
            Int32 edgesCount;
            bool have = false;
            for (int i = 0; i < count; i++)
            {
                if (Genom[i] == Genom[i + 1]) return false;
                edgesCount = nodes[Genom[i]].EdgesId.Length;
                have = false;
                for (int y = 0; y < edgesCount; y++)
                    if (edges[nodes[Genom[i]].EdgesId[y]].HaveNode(Genom[i + 1]))
                        have = true;

                if (!have) return false;
            }
            count++;
            List<int> FindNodes = new List<int>();
            for (int i = 0; i < count; i++)
                if (!FindNodes.Contains(Genom[i]))
                    FindNodes.Add(Genom[i]);

            return FindNodes.Count == nodes.Length;
        }
    }
    public class EvolutionEngine
    {
        static Object randLock, individualsLock;
        static Random rand;
        public List<Edge> edges;
        public List<Node> nodes;
        private List<Dna> individuals;
        public Int32 callbackStep = 1;
        public event Action<string, string, string> UpdateGenerationStep;
        public EvolutionEngine()
        {
            rand = new Random();
            randLock = new Object();
            individualsLock = new Object();
            edges = new List<Edge>();
            nodes = new List<Node>();
        }
        public bool Start(Int32 population, Int32 generations, Int32 startNode, Int32 ver)
        {
            Edge[] srcEdge = edges.ToArray();
            Node[] srcNode = nodes.ToArray();
            individuals = new List<Dna>(population);
            List<Dna> nexIndividuals = new List<Dna>(population);
            Int32 populationLen;
            Parallel.For(0, population, x =>
            {
                var tmp = Dna.New(srcNode, startNode, rand, randLock);
                lock (individualsLock) individuals.Add(tmp);
            });
            population /= 2;
            for (int i = 0; i < generations; i++)
            {
                individuals = individuals.AsParallel().Where(x => x.Validate(srcEdge, srcNode)).OrderBy(x => x.Length(srcEdge)).ToList();
                if (i % callbackStep == 0)
                {
                    if (individuals.Count != 0)
                        UpdateGenerationStep?.Invoke(i.ToString(), individuals.First().Length(srcEdge).ToString(), individuals.AsParallel().Average(x => x.Length(srcEdge)).ToString());
                    else
                        UpdateGenerationStep?.Invoke(i.ToString(), "NaN", "NaN");
                }
                if (individuals.Count == 0)
                {
                    individuals.Clear();
                    Parallel.For(0, population, x =>
                    {
                        var tmp = Dna.New(srcNode, startNode, rand, randLock);
                        lock (individualsLock) individuals.Add(tmp);
                    });
                }
                populationLen = individuals.Count;
                nexIndividuals = new List<Dna>(individuals.Take(populationLen >> 1));
                Parallel.For(0, population >> 1, x =>
                {
                    Int32 ia, ib;
                    lock (randLock)
                    {
                        ia = rand.Next(0, populationLen >> 1);
                        ib = rand.Next(0, populationLen >> 1);
                    }
                    if (ia == ib) ib = (ib + 1) % populationLen;
                    var tmp = Dna.Cross(individuals[ia], individuals[ib]);
                    lock (individualsLock) nexIndividuals.Add(tmp);
                    tmp = Dna.Cross(individuals[ib], individuals[ia]);
                    lock (individualsLock) nexIndividuals.Add(tmp);
                    tmp = Dna.New(srcNode, startNode, rand, randLock);
                    lock (individualsLock) nexIndividuals.Add(tmp);
                    tmp = Dna.New(srcNode, startNode, rand, randLock);
                    lock (individualsLock) nexIndividuals.Add(tmp);
                });
                individuals = new List<Dna>(nexIndividuals);
                Parallel.For(0, individuals.Count, x => individuals[x].Mutate(ver, rand, randLock, srcNode));
            }
            individuals = individuals.AsParallel().Where(x => x.Validate(srcEdge, srcNode)).OrderBy(x => x.Length(srcEdge)).ToList();
            if (individuals.Count == 0) return false;
            return true;
        }
        public Dna Best() => individuals.FirstOrDefault();
        public void AddEdge(Int32 nodeA, Int32 nodeB, Int32 len) => edges.Add(new Edge() { Id = edges.Count, NodeAId = nodeA, NodeBId = nodeB, Length = len });
        public void ClearEdges() => edges.Clear();
        public void InitNodes(int count)
        {
            nodes.Clear();
            for (int i = 0; i < count; i++)
                nodes.Add(new Node() { Id = i });
        }
        public void Init()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var item = nodes[i];
                item.EdgesId = edges.Where(x => x.HaveNode(item.Id)).Select(x => x.Id).ToArray();
                List<Int32> ids = new List<Int32>(item.EdgesId.Length);
                foreach (var item2 in item.EdgesId)
                    ids.Add(edges[item2].GetAnother(item.Id));
                item.NodesId = ids.ToArray();
                nodes[i] = item;
            }
        }
    }
}