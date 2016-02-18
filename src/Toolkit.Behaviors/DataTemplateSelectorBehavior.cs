using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Toolkit.Behaviors
{
    public class DataTemplateSelectorBehavior : Behavior<ListViewBase>
    {
        private Dictionary<string, DataTemplate> _typeToTemplateMapping;
        private Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping;
        private SelectorItemType _itemType = SelectorItemType.GridViewItem;

        public DataTemplateSelectorBehavior()
        {
            Mappings = new DataTemplateMappingCollection();
            _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
            _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();
        }

        private enum SelectorItemType
        {
            GridViewItem,
            ListViewItem
        }

        public DataTemplateMappingCollection Mappings { get; }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.ChoosingItemContainer += OnChoosingItemContainer;
            AssociatedObject.ContainerContentChanging += OnContainerContentChanging;
            ProcessMappings();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.ContainerContentChanging -= OnContainerContentChanging;
        }

        private void AddTypeMapping(DataTemplateMapping mapping)
        {
            _typeToTemplateMapping.Add(mapping.TypeName, mapping.Template);
            _typeToItemHashSetMapping.Add(mapping.TypeName, new HashSet<SelectorItem>());
            var hashSet = _typeToItemHashSetMapping[mapping.TypeName];
            for (int i = 0; i < mapping.CacheLength; i++)
            {
                SelectorItem item = null;
                if (AssociatedObject is GridView)
                {
                    item = new GridViewItem();
                }
                else
                {
                    item = new ListViewItem();
                }

                item.ContentTemplate = _typeToTemplateMapping[mapping.TypeName];
                item.Tag = mapping.TypeName;
                hashSet.Add(item);
                Debug.WriteLine($"Adding {item.GetHashCode()} to {mapping.TypeName}");
            }
        }

        private void ProcessMappings()
        {
            foreach (var item in Mappings)
            {
                AddTypeMapping(item);
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item.GetType().Name;

            // TODO: retrieve this safely
            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer != null)
            {
                if (args.ItemContainer.Tag.Equals(typeName))
                {
                    relevantHashSet.Remove(args.ItemContainer);
                    Debug.WriteLine($"Removing {args.ItemContainer.GetHashCode()} from {typeName}");
                }
                else
                {
                    // The ItemContainer's datatemplate does not match the needed
                    // datatemplate.
                    args.ItemContainer = null;
                }
            }

            if (args.ItemContainer == null)
            {
                // See if we can fetch from the correct list.
                if (relevantHashSet.Count > 0)
                {
                    // Unfortunately have to resort to LINQ here. There's no efficient way of getting an arbitrary
                    // item from a hashset without knowing the item. Queue isn't usable for this scenario
                    // because you can't remove a specific element (which is needed in the block above).
                    args.ItemContainer = relevantHashSet.First();
                    relevantHashSet.Remove(args.ItemContainer);
                    Debug.WriteLine($"Removing {args.ItemContainer.GetHashCode()} from {typeName}");
                }
                else
                {
                    // There aren't any (recycled) ItemContainers available. So a new one
                    // needs to be created.
                    SelectorItem item = null;
                    if (_itemType == SelectorItemType.GridViewItem)
                    {
                        item = new GridViewItem();
                    }
                    else
                    {
                        item = new ListViewItem();
                    }

                    item.ContentTemplate = _typeToTemplateMapping[typeName];
                    item.Tag = typeName;
                    args.ItemContainer = item;
                    Debug.WriteLine($"Creating {args.ItemContainer.GetHashCode()} for {typeName}");
                }
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue == true)
            {
                var tag = args.ItemContainer.Tag as string;

                Debug.WriteLine($"Adding {args.ItemContainer.GetHashCode()} to {tag}");

                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

                Debug.Assert(added == true, "Recycle queue should never have dupes. If so, we may be incorrectly reusing a container that is already in use!");
            }
        }
    }
}
