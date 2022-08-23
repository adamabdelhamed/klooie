using System;

namespace klooie;
public abstract class CollectionDataSource
{
    public event Action DataChanged;
    public abstract CollectionDataView GetDataView(CollectionQuery query);
    public abstract int GetHighestKnownIndex(CollectionQuery query);

    public void FireDataChanged()
    {
        if (DataChanged != null)
        {
            DataChanged();
        }
    }

    public abstract void ClearCachedData();
}
