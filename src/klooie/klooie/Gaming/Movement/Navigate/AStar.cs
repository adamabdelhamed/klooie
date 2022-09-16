namespace klooie.Gaming;
public static class AStar
{
#if DEBUG
	public static int CallCount = 0;
#endif

	static HashSet<Node> closedSet = new HashSet<Node>();

	/*
		todo - this currently only works because the grid units are 1 x 1,
			   but if a larger rectangle comes in we could scale the grid so
			   that each grid unit is the size of the incoming rectangle and
	           then leave the algorithm as is
	*/
	public static List<LocF> FindPath(int worldWidth, int worldHeight, RectF startPos, RectF targetPos, List<RectF> obstacles)
	{
#if DEBUG
		CallCount++;
#endif

		var grid = GridPool.Instance.Get(worldWidth, worldHeight, obstacles);
		var startNode = grid.nodes[(int)startPos.CenterX][(int)startPos.CenterY];
		var targetNode = grid.nodes[(int)targetPos.CenterX][(int)targetPos.CenterY];

		Heap openSet = HeapPool.Instance.Get(grid.MaxSize);
		closedSet.Clear();
		openSet.Add(startNode);

		while (openSet.Count > 0)
		{
			Node currentNode = openSet.RemoveFirst();
			closedSet.Add(currentNode);

			if (currentNode == targetNode)
			{
				HeapPool.Instance.Return(openSet);
				GridPool.Instance.Return(grid);
				return RetracePath(startNode, targetNode);
			}

			var neighbors = grid.GetNeighbours(currentNode).AsSpan();
			for(var i = 0; i < neighbors.Length; i++)
			{
				var neighbour = neighbors[i];
				if (neighbour == null || !neighbour.walkable || closedSet.Contains(neighbour))
				{
					continue;
				}

				int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
				if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
				{
					neighbour.gCost = newMovementCostToNeighbour;
					neighbour.hCost = GetDistance(neighbour, targetNode);
					neighbour.parent = currentNode;

					if (!openSet.Contains(neighbour))
					{
						openSet.Add(neighbour);
					}
				}
			}
		}
		HeapPool.Instance.Return(openSet);
		GridPool.Instance.Return(grid);
		return null;
	}

	private static List<LocF> RetracePath(Node startNode, Node endNode)
	{
		List<LocF> path = new List<LocF>();
		Node currentNode = endNode;

		while (currentNode != startNode)
		{
			path.Add(new LocF(currentNode.Left, currentNode.Top));
			currentNode = currentNode.parent;
		}
		path.Reverse();

		for(var i = 1; i < path.Count -1; i++)
        {
			var previousNode = path[i - 1];
			var thisNode = path[i];
			var nextNode = path[i+1];

			var areVerticallyStacked = thisNode.Left == previousNode.Left && thisNode.Left == nextNode.Left;
			var areHorizontallyStacked = thisNode.Top == previousNode.Top && thisNode.Top == nextNode.Top;

			var isLateralOrVericalMove = areVerticallyStacked || areHorizontallyStacked;
			if(isLateralOrVericalMove)
            {
				path.RemoveAt(i--);
            }
		}

		return path;

	}

	private static int GetDistance(Node nodeA, Node nodeB)
	{
		int dstX = Math.Abs(nodeA.Left - nodeB.Left);
		int dstY = 2 * Math.Abs(nodeA.Top - nodeB.Top);

		if (dstX > dstY)
			return 14 * dstY + 10 * (dstX - dstY);
		return 14 * dstX + 10 * (dstY - dstX);
	}

	private class Grid
	{
		public Node[][] nodes;
		public int w;
		public int h;
		public Grid(int w, int h, List<RectF> obstacles)
		{
			this.w = w;
			this.h = h;
			CreateGrid();
			InitializeGrid(obstacles);
		}

		public void Reset(List<RectF> obstacles)
		{
			for (int x = 0; x < w; x++)
			{
				var xSpan = nodes[x].AsSpan();
				for (int y = 0; y < h; y++)
				{
					xSpan[y].Reset();
				}
			}

			InitializeGrid(obstacles);
		}

		public int MaxSize => w * h;

		void CreateGrid()
		{
			nodes = new Node[w][];

			for (int x = 0; x < w; x++)
			{
				nodes[x] = new Node[h];
				for (int y = 0; y < h; y++)
				{
					nodes[x][y] = new Node(x, y);
				}
			}
		}

		public void InitializeGrid(List<RectF> obstacles)
		{
            for (int i = 0; i < obstacles.Count; i++)
			{
                RectF obstacle = obstacles[i];

				var left = (int)Math.Max(obstacle.Left, 0);
				var right = (int)Math.Min(this.w - 1, obstacle.Right);

				var top = (int)Math.Max(obstacle.Top, 0);
				var bottom = (int)Math.Min(this.h - 1, obstacle.Bottom);

				for (var x = left; x <= right; x++)
				{
					var xSpan = nodes[x].AsSpan();
					for (var y = top; y <= bottom; y++)
					{
						if (x >= 0 && y >= 0 && x < w && y < h)
						{
							xSpan[y].walkable = false;
						}
					}
				}
			}
		}
		Node[] neighbors = new Node[8];
		public Node[] GetNeighbours(Node node)
		{
			var ni = 0;
			for (int x = -1; x <= 1; x++)
			{
				for (int y = -1; y <= 1; y++)
				{
					if (x == 0 && y == 0) continue;

					int checkX = node.Left + x;
					int checkY = node.Top + y;

					neighbors[ni++] = checkX >= 0 && checkX < w && checkY >= 0 && checkY < h ? nodes[checkX][checkY] : null;
				}
			}
			return neighbors;
		}
	}

	private class Node
	{
		public bool walkable;
		public int Left;
		public int Top;
		public int gCost;
		public int hCost;
		public Node parent;
		public int HeapIndex;
		public Node(int left, int top)
		{
			Left = left;
			Top = top;
			walkable = true;
		}

		public void Reset()
		{
			walkable = true;
			gCost = default;
			hCost = default;
			parent = null;
			HeapIndex = default;
		}

		public int CompareTo(Node nodeToCompare)
		{
			int compare = (gCost + hCost).CompareTo(nodeToCompare.gCost + nodeToCompare.hCost);
			if (compare == 0)
			{
				compare = hCost.CompareTo(nodeToCompare.hCost);
			}
			return -compare;
		}
	}

	private class Heap
	{
		public Node[] items;
		int currentItemCount;

		public Heap(int maxHeapSize)
		{
			items = new Node[maxHeapSize];
		}

		public void Reset()
        {
			Array.Clear(items);
			currentItemCount = default;
		}

		public void Add(Node item)
		{
			item.HeapIndex = currentItemCount;
			items[currentItemCount] = item;
			SortUp(item);
			currentItemCount++;
		}

		public Node RemoveFirst()
		{
			Node firstItem = items[0];
			currentItemCount--;
			items[0] = items[currentItemCount];
			items[0].HeapIndex = 0;
			SortDown(items[0]);
			return firstItem;
		}

		public void UpdateItem(Node item)
		{
			SortUp(item);
		}

		public int Count
		{
			get
			{
				return currentItemCount;
			}
		}

		public bool Contains(Node item)
		{
			return Equals(items[item.HeapIndex], item);
		}

		void SortDown(Node item)
		{
			while (true)
			{
				int childIndexLeft = item.HeapIndex * 2 + 1;
				int childIndexRight = item.HeapIndex * 2 + 2;
				int swapIndex = 0;

				if (childIndexLeft < currentItemCount)
				{
					swapIndex = childIndexLeft;

					if (childIndexRight < currentItemCount)
					{
						if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0)
						{
							swapIndex = childIndexRight;
						}
					}

					if (item.CompareTo(items[swapIndex]) < 0)
					{
						Swap(item, items[swapIndex]);
					}
					else
					{
						return;
					}

				}
				else
				{
					return;
				}

			}
		}

		void SortUp(Node item)
		{
			int parentIndex = (item.HeapIndex - 1) / 2;

			while (true)
			{
				Node parentItem = items[parentIndex];
				if (item.CompareTo(parentItem) > 0)
				{
					Swap(item, parentItem);
				}
				else
				{
					break;
				}

				parentIndex = (item.HeapIndex - 1) / 2;
			}
		}

		void Swap(Node itemA, Node itemB)
		{
			items[itemA.HeapIndex] = itemB;
			items[itemB.HeapIndex] = itemA;
			int itemAIndex = itemA.HeapIndex;
			itemA.HeapIndex = itemB.HeapIndex;
			itemB.HeapIndex = itemAIndex;
		}
	}

	private class HeapPool
	{
#if DEBUG
    internal static int HitCount = 0;
    internal static int MissCount = 0;
    internal static int PartialHitCount = 0;
#endif
		private List<Heap> pool = new List<Heap>();
		public static readonly HeapPool Instance = new HeapPool();

		public Heap Get(int maxSize)
		{
			// try to find an existing buffer that is big enough
			for (var i = 0; i < pool.Count; i++)
			{
				if (pool[i].items.Length == maxSize)
				{
					var toRent = pool[i];
					pool.RemoveAt(i);
#if DEBUG
                    HitCount++;
#endif
					toRent.Reset();
					return toRent;
				}
			}

#if DEBUG
                MissCount++;
#endif
			return new Heap(maxSize);

		}

		public void Return(Heap h)
		{
			pool.Add(h);
		}
	}

	private class GridPool
	{
#if DEBUG
    internal static int HitCount = 0;
    internal static int MissCount = 0;
    internal static int PartialHitCount = 0;
#endif
		private List<Grid> pool = new List<Grid>();
		public static readonly GridPool Instance = new GridPool();

		public Grid Get(int w, int h, List<RectF> obstacles)
		{
			// try to find an existing buffer that is big enough
			for (var i = 0; i < pool.Count; i++)
			{
				if (pool[i].w == w && pool[i].h == h)
				{
					var toRent = pool[i];
					pool.RemoveAt(i);
#if DEBUG
                    HitCount++;
#endif
					toRent.Reset(obstacles);
					return toRent;
				}
			}

#if DEBUG
                MissCount++;
#endif
			return new Grid(w, h, obstacles);

		}

		public void Return(Grid g)
		{
			pool.Add(g);
		}
	}
}