////#define ENABLE_DEBUG_SPEW
//// Uncomment the above to enable debug spew. Disabling by default as this slows down performance in debug mode

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
using Windows.UI.Xaml.Markup;

namespace Toolkit.Behaviors
{
    [ContentProperty(Name = nameof(Mappings))]
    public class DataTemplateSelectorBehavior : Behavior<ListViewBase>
    {
        public static readonly DependencyProperty MappingsProperty =
            DependencyProperty.Register(nameof(Mappings), typeof(DependencyObjectCollection), typeof(DataTemplateSelectorBehavior), new PropertyMetadata(null));

        private Dictionary<string, DataTemplate> _typeToTemplateMapping;
        private Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping;
        private SelectorItemType _itemType = SelectorItemType.GridViewItem;

        public DataTemplateSelectorBehavior()
        {
            _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
            _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();
            DisableDataContext = false;
        }

        private enum SelectorItemType
        {
            GridViewItem,
            ListViewItem
        }

        public DependencyObjectCollection Mappings
        {
            get
            {
                var mappings = (DependencyObjectCollection)GetValue(MappingsProperty);
                if (mappings == null)
                {
                    mappings = new DependencyObjectCollection();
                    SetValue(MappingsProperty, mappings);
                }

                return mappings;
            }

            set
            {
                SetValue(MappingsProperty, value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to apply DataContext.
        /// Set to True if the DataTemplates are *not* using {Binding}
        /// and are only using x:Bind. This will provide a performance boost.
        /// </summary>
        public bool DisableDataContext { get; set; }

        protected override void OnAttached()
        {
            base.OnAttached();
            _itemType = AssociatedObject is GridView ? SelectorItemType.GridViewItem : SelectorItemType.ListViewItem;
            AssociatedObject.ChoosingItemContainer += OnChoosingItemContainer;
            AssociatedObject.ContainerContentChanging += OnContainerContentChanging;
            ProcessMappings();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.ContainerContentChanging -= OnContainerContentChanging;
            AssociatedObject.ChoosingItemContainer -= OnChoosingItemContainer;
        }

        private void ProcessMappings()
        {
            foreach (DataTemplateMapping item in Mappings)
            {
                AddTypeMapping(item);
            }
        }

        private void AddTypeMapping(DataTemplateMapping mapping)
        {
            _typeToTemplateMapping.Add(mapping.TypeName, mapping.Template);
            _typeToItemHashSetMapping.Add(mapping.TypeName, new HashSet<SelectorItem>());
            var hashSet = _typeToItemHashSetMapping[mapping.TypeName];
            for (int i = 0; i < mapping.CacheLength; i++)
            {
                var item = CreateSelectorItem(mapping.TypeName);
                hashSet.Add(item);
#if ENABLE_DEBUG_SPEW
                Debug.WriteLine($"Adding {item.GetHashCode()} to {mapping.TypeName}");
#endif // ENABLE_DEBUG_SPEW
            }
        }

        private SelectorItem CreateSelectorItem(string typeName)
        {
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
            return item;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item.GetType().Name;

            Debug.Assert(_typeToItemHashSetMapping.ContainsKey(typeName), "The type of the item used with DataTemplateSelectorBehavior must have a DataTemplate mapping");
            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer != null)
            {
                if (args.ItemContainer.Tag.Equals(typeName))
                {
                    // Suggestion matches what we want, so remove it from the recycle queue
                    relevantHashSet.Remove(args.ItemContainer);
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Removing (suggested) {args.ItemContainer.GetHashCode()} from {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
                else
                {
                    // The ItemContainer's datatemplate does not match the needed
                    // datatemplate.
                    // Don't remove it from the recycle queue, since XAML will resuggest it later
                    args.ItemContainer = null;
                }
            }

            // If there was no suggested container or XAML's suggestion was a miss, pick one up from the recycle queue
            // or create a new one
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
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Removing (reused) {args.ItemContainer.GetHashCode()} from {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
                else
                {
                    // There aren't any (recycled) ItemContainers available. So a new one
                    // needs to be created.
                    var item = CreateSelectorItem(typeName);
                    args.ItemContainer = item;
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Creating {args.ItemContainer.GetHashCode()} for {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue == true)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;

#if ENABLE_DEBUG_SPEW
                Debug.WriteLine($"Adding {args.ItemContainer.GetHashCode()} to {tag}");
#endif // ENABLE_DEBUG_SPEW

                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

#if ENABLE_DEBUG_SPEW
                Debug.Assert(added == true, "Recycle queue should never have dupes. If so, we may be incorrectly reusing a container that is already in use!");
#endif // ENABLE_DEBUG_SPEW
            }

            if (DisableDataContext == true)
            {
                // Setting args.Handled to true tells XAML we're not using
                // {Binding}, so there's no need to apply DataContext.
                // This results in a boost to performance.
                args.Handled = true;
            }
        }
    }
}
