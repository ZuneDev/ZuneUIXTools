﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Accessibility.Accessible
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Library;
using Microsoft.Iris.Markup;
using Microsoft.Iris.ModelItems;
using Microsoft.Iris.Session;

namespace Microsoft.Iris.Accessibility
{
    public class Accessible : NotifyObjectBase
    {
        private DynamicData _dataMap;
        private AccessibleProxy _proxy;
        private static readonly DataCookie s_descriptionSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_defaultActionSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_defaultActionCommandSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_helpSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_helpTopicSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_keyboardShortcutSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_nameSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_roleSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_valueSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_animatedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_unavailableStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_selectedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_busyStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_pressedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_checkedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_collapsedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_defaultStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_marqueeStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_mixedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_expandedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_traversedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_selectableStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_protectedStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_popupStateSlot = DataCookie.ReserveSlot();
        private static readonly DataCookie s_multiSelectableStateSlot = DataCookie.ReserveSlot();

        public Accessible() => this.SetData(s_roleSlot, AccRole.Client);

        public void Attach(AccessibleProxy proxy)
        {
            this._proxy = proxy;
            this.FireNotification(NotificationID.Enabled);
        }

        public void Detach()
        {
            this.SetData(s_defaultActionCommandSlot, null);
            this._proxy = null;
        }

        public bool Enabled => this._proxy != null;

        public string Description
        {
            get => (string)this.GetData(s_descriptionSlot);
            set
            {
                string description = this.Description;
                if (!(value != description))
                    return;
                this.SetData(s_descriptionSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.Description, AccessibleProperty.Description);
            }
        }

        public string DefaultAction
        {
            get => (string)this.GetData(s_defaultActionSlot);
            set
            {
                string defaultAction = this.DefaultAction;
                if (!(value != defaultAction))
                    return;
                this.SetData(s_defaultActionSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.DefaultAction, AccessibleProperty.DefaultAction);
            }
        }

        public IUICommand DefaultActionCommand
        {
            get => (IUICommand)this.GetData(s_defaultActionCommandSlot);
            set
            {
                IUICommand defaultActionCommand = this.DefaultActionCommand;
                if (value == defaultActionCommand)
                    return;
                this.SetData(s_defaultActionCommandSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.DefaultActionCommand, AccessibleProperty.DefaultActionCommand);
            }
        }

        public string Help
        {
            get => (string)this.GetData(s_helpSlot);
            set
            {
                string help = this.Help;
                if (!(value != help))
                    return;
                this.SetData(s_helpSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.Help, AccessibleProperty.Help);
            }
        }

        public int HelpTopic
        {
            get
            {
                object data = this.GetData(s_helpTopicSlot);
                return data != null ? (int)data : -1;
            }
            set
            {
                int helpTopic = this.HelpTopic;
                if (value == helpTopic)
                    return;
                this.SetData(s_helpTopicSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.HelpTopic, AccessibleProperty.HelpTopic);
            }
        }

        public string KeyboardShortcut
        {
            get => (string)this.GetData(s_keyboardShortcutSlot);
            set
            {
                string keyboardShortcut = this.KeyboardShortcut;
                if (!(value != keyboardShortcut))
                    return;
                this.SetData(s_keyboardShortcutSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.KeyboardShortcut, AccessibleProperty.KeyboardShortcut);
            }
        }

        public string Name
        {
            get => (string)this.GetData(s_nameSlot);
            set
            {
                string name = this.Name;
                if (!(value != name))
                    return;
                this.SetData(s_nameSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.Name, AccessibleProperty.Name);
            }
        }

        public AccRole Role
        {
            get
            {
                object data = this.GetData(s_roleSlot);
                return data != null ? (AccRole)data : AccRole.None;
            }
            set
            {
                AccRole role = this.Role;
                if (value == role)
                    return;
                this.SetData(s_roleSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.Role, AccessibleProperty.Role);
            }
        }

        public string Value
        {
            get => (string)this.GetData(s_valueSlot);
            set
            {
                string str = this.Value;
                if (!(value != str))
                    return;
                this.SetData(s_valueSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.Value, AccessibleProperty.Value);
            }
        }

        public bool IsAnimated
        {
            get
            {
                object data = this.GetData(s_animatedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isAnimated = this.IsAnimated;
                if (value == isAnimated)
                    return;
                this.SetData(s_animatedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsAnimated, AccessibleProperty.IsAnimated);
            }
        }

        public bool IsUnavailable
        {
            get
            {
                object data = this.GetData(s_unavailableStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isUnavailable = this.IsUnavailable;
                if (value == isUnavailable)
                    return;
                this.SetData(s_unavailableStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsUnavailable, AccessibleProperty.IsUnavailable);
            }
        }

        public bool IsSelected
        {
            get
            {
                object data = this.GetData(s_selectedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isSelected = this.IsSelected;
                if (value == isSelected)
                    return;
                this.SetData(s_selectedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsSelected, AccessibleProperty.IsSelected);
            }
        }

        public bool IsBusy
        {
            get
            {
                object data = this.GetData(s_busyStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isBusy = this.IsBusy;
                if (value == isBusy)
                    return;
                this.SetData(s_busyStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsBusy, AccessibleProperty.IsBusy);
            }
        }

        public bool IsPressed
        {
            get
            {
                object data = this.GetData(s_pressedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isPressed = this.IsPressed;
                if (value == isPressed)
                    return;
                this.SetData(s_pressedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsPressed, AccessibleProperty.IsPressed);
            }
        }

        public bool IsChecked
        {
            get
            {
                object data = this.GetData(s_checkedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isChecked = this.IsChecked;
                if (value == isChecked)
                    return;
                this.SetData(s_checkedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsChecked, AccessibleProperty.IsChecked);
            }
        }

        public bool IsCollapsed
        {
            get
            {
                object data = this.GetData(s_collapsedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isCollapsed = this.IsCollapsed;
                if (value == isCollapsed)
                    return;
                this.SetData(s_collapsedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsCollapsed, AccessibleProperty.IsCollapsed);
            }
        }

        public bool IsDefault
        {
            get
            {
                object data = this.GetData(s_defaultStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isDefault = this.IsDefault;
                if (value == isDefault)
                    return;
                this.SetData(s_defaultStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsDefault, AccessibleProperty.IsDefault);
            }
        }

        public bool IsMarquee
        {
            get
            {
                object data = this.GetData(s_marqueeStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isMarquee = this.IsMarquee;
                if (value == isMarquee)
                    return;
                this.SetData(s_marqueeStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsMarquee, AccessibleProperty.IsMarquee);
            }
        }

        public bool IsMixed
        {
            get
            {
                object data = this.GetData(s_mixedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isMixed = this.IsMixed;
                if (value == isMixed)
                    return;
                this.SetData(s_mixedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsMixed, AccessibleProperty.IsMixed);
            }
        }

        public bool IsExpanded
        {
            get
            {
                object data = this.GetData(s_expandedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isExpanded = this.IsExpanded;
                if (value == isExpanded)
                    return;
                this.SetData(s_expandedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsExpanded, AccessibleProperty.IsExpanded);
            }
        }

        public bool IsTraversed
        {
            get
            {
                object data = this.GetData(s_traversedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isTraversed = this.IsTraversed;
                if (value == isTraversed)
                    return;
                this.SetData(s_traversedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsTraversed, AccessibleProperty.IsTraversed);
            }
        }

        public bool IsSelectable
        {
            get
            {
                object data = this.GetData(s_selectableStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isSelectable = this.IsSelectable;
                if (value == isSelectable)
                    return;
                this.SetData(s_selectableStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsSelectable, AccessibleProperty.IsSelectable);
            }
        }

        public bool IsMultiSelectable
        {
            get
            {
                object data = this.GetData(s_multiSelectableStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isMultiSelectable = this.IsMultiSelectable;
                if (value == isMultiSelectable)
                    return;
                this.SetData(s_multiSelectableStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsMultiSelectable, AccessibleProperty.IsMultiSelectable);
            }
        }

        public bool IsProtected
        {
            get
            {
                object data = this.GetData(s_protectedStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool isProtected = this.IsProtected;
                if (value == isProtected)
                    return;
                this.SetData(s_protectedStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.IsProtected, AccessibleProperty.IsProtected);
            }
        }

        public bool HasPopup
        {
            get
            {
                object data = this.GetData(s_popupStateSlot);
                return data != null && (bool)data;
            }
            set
            {
                bool hasPopup = this.HasPopup;
                if (value == hasPopup)
                    return;
                this.SetData(s_popupStateSlot, value);
                this.FireAccessiblePropertyChanged(NotificationID.HasPopup, AccessibleProperty.HasPopup);
            }
        }

        protected void FireAccessiblePropertyChanged(
          string propertyName,
          AccessibleProperty accessibleProperty)
        {
            this.FireNotification(propertyName);
            if (this._proxy != null)
            {
                this._proxy.NotifyAccessiblePropertyChanged(accessibleProperty);
            }
            else
            {
                if (accessibleProperty == AccessibleProperty.Name)
                    return;
                ErrorManager.ReportWarning("Accessibility: Script modifications to the 'Accessible' object ('{0}' property) detected even though an Accessibility client is not is use. Use 'if (Accessible.Enabled) {{ ... }}' to bypass Accessible property access in this case", propertyName);
            }
        }

        protected object GetData(DataCookie cookie) => this._dataMap.GetData(cookie);

        protected void SetData(DataCookie cookie, object value) => this._dataMap.SetData(cookie, value);
    }
}
