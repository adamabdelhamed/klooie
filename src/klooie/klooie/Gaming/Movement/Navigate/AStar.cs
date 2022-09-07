namespace klooie.Gaming;
public static class AStar
{
	public static async Task<List<LocF>> FindPath(int worldWidth, int worldHeight, RectF startPos, RectF targetPos, List<RectF> obstacles, bool debug)
	{
		using (var algoLt = Game.Current.CreateChildLifetime())
		{
			if (debug)
			{
				Debugger.HighlightCell((int)startPos.CenterX, (int)startPos.CenterY, algoLt, new ConsoleCharacter('A', RGB.Green), z: int.MaxValue);
				Debugger.HighlightCell((int)targetPos.CenterX, (int)targetPos.CenterY, algoLt, new ConsoleCharacter('B', RGB.Red), z: int.MaxValue);
				await Game.Current.Delay(100);
			}

			var grid = new Grid(worldWidth, worldHeight, obstacles, debug ? algoLt : null);
			var startNode = grid.nodes[(int)startPos.CenterX, (int)startPos.CenterY];
			var targetNode = grid.nodes[(int)targetPos.CenterX, (int)targetPos.CenterY];

			Heap<Node> openSet = new Heap<Node>(grid.MaxSize);
			HashSet<Node> closedSet = new HashSet<Node>();
			openSet.Add(startNode);
			var iters = 0;
			while (openSet.Count > 0)
			{
				Node currentNode = openSet.RemoveFirst();

				if (debug)
				{
					Debugger.HighlightCell(currentNode.Left, currentNode.Top, algoLt, new ConsoleCharacter('.', RGB.Gray), z: -1000);
					iters++;
					if (iters % 20 == 0)
					{
						await Task.Yield();
					}
				}

				closedSet.Add(currentNode);

				if (currentNode == targetNode)
				{
					return RetracePath(startNode, targetNode);
				}

				foreach (Node neighbour in grid.GetNeighbours(currentNode))
				{
					if (!neighbour.walkable || closedSet.Contains(neighbour))
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
			return null;
		}
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
		public Node[,] nodes;
		private ILifetimeManager algoLt;
		private int w;
		private int h;
		public Grid(int w, int h, List<RectF> obstacles, ILifetimeManager algoLt)
		{
			this.w = w;
			this.h = h;
			this.algoLt = algoLt;
			CreateGrid(obstacles);
		}

		public int MaxSize => w * h;

		void CreateGrid(List<RectF> obstacles)
		{
			nodes = new Node[w, h];
				 
			for (int x = 0; x < w; x++)
			{
				for (int y = 0; y < h; y++)
				{
					nodes[x, y] = new Node(x, y);
				}
			}

			foreach (var obstacle in obstacles)
            {
				for(var x = (int)Math.Floor(obstacle.Left); x <= (int)Math.Ceiling(obstacle.Right); x++)
                {
					for (var y = (int)Math.Floor(obstacle.Top); y <= (int)Math.Ceiling(obstacle.Bottom); y++)
					{
						if (x >= 0 && y >= 0 && x < w && y < h)
						{
							nodes[x, y].walkable = false;
							if(algoLt != null)
                            {
								Debugger.HighlightCell(x, y, algoLt, new ConsoleCharacter(' ', backgroundColor: RGB.Red), z: int.MaxValue);
                            }
						}
					}
				}
            }
		}

		public List<Node> GetNeighbours(Node node)
		{
			List<Node> neighbours = new List<Node>();

			for (int x = -1; x <= 1; x++)
			{
				for (int y = -1; y <= 1; y++)
				{
					if (x == 0 && y == 0)
						continue;

					int checkX = node.Left + x;
					int checkY = node.Top + y;

					if (checkX >= 0 && checkX < w && checkY >= 0 && checkY < h)
					{
						neighbours.Add(nodes[checkX, checkY]);
					}
				}
			}

			return neighbours;
		}
	}

	private class Node : IHeapItem<Node>
	{
		public bool walkable;
		public int Left;
		public int Top;
		public int gCost;
		public int hCost;
		public Node parent;
		public Node(int left, int top)
		{
			Left = left;
			Top = top;
			walkable = true;
		}

		public int fCost
		{
			get
			{
				return gCost + hCost;
			}
		}

		public int HeapIndex { get; set; }

		public int CompareTo(Node nodeToCompare)
		{
			int compare = fCost.CompareTo(nodeToCompare.fCost);
			if (compare == 0)
			{
				compare = hCost.CompareTo(nodeToCompare.hCost);
			}
			return -compare;
		}
	}

	private class Heap<T> where T : IHeapItem<T>
	{

		T[] items;
		int currentItemCount;

		public Heap(int maxHeapSize)
		{
			items = new T[maxHeapSize];
		}

		public void Add(T item)
		{
			item.HeapIndex = currentItemCount;
			items[currentItemCount] = item;
			SortUp(item);
			currentItemCount++;
		}

		public T RemoveFirst()
		{
			T firstItem = items[0];
			currentItemCount--;
			items[0] = items[currentItemCount];
			items[0].HeapIndex = 0;
			SortDown(items[0]);
			return firstItem;
		}

		public void UpdateItem(T item)
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

		public bool Contains(T item)
		{
			return Equals(items[item.HeapIndex], item);
		}

		void SortDown(T item)
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

		void SortUp(T item)
		{
			int parentIndex = (item.HeapIndex - 1) / 2;

			while (true)
			{
				T parentItem = items[parentIndex];
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

		void Swap(T itemA, T itemB)
		{
			items[itemA.HeapIndex] = itemB;
			items[itemB.HeapIndex] = itemA;
			int itemAIndex = itemA.HeapIndex;
			itemA.HeapIndex = itemB.HeapIndex;
			itemB.HeapIndex = itemAIndex;
		}
	}

	private interface IHeapItem<T> : IComparable<T>
	{
		int HeapIndex
		{
			get;
			set;
		}
	}
}