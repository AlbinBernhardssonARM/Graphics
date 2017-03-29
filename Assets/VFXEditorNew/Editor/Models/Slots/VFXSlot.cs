using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Graphing;

namespace UnityEditor.VFX
{
    [Serializable]
    class VFXSlot : VFXModel<VFXSlot, VFXSlot>
    {
        public enum Direction
        {
            kInput,
            kOutput,
        }

        public Direction direction { get { return m_Direction; } }
        public VFXProperty property { get { return m_Property; } }
        public override string name { get { return m_Property.name; } }

        public VFXExpression expression 
        {
            set { SetExpression(value); }
            get 
            {
                if (!m_Initialize)
                    RecomputeExpressionTree(false, false);
                return m_OutExpression; 
            }
        }

        // Explicit setter to be able to not notify
        public void SetExpression(VFXExpression expr, bool notify = true)
        {
            //if (direction == Direction.kInput)
            //    throw new InvalidOperationException("Explicit SetExpression can only be called on output slots");

            if (m_LinkedInExpression != expr)
            {
                m_LinkedInExpression = expr;
                RecomputeExpressionTree(true,notify);
            }
        }

        public ReadOnlyCollection<VFXSlot> LinkedSlots
        {
            get
            {
                return m_LinkedSlots.AsReadOnly();
            }
        }

        public VFXSlot refSlot
        { 
            get 
            {
                if (direction == Direction.kOutput || !HasLink())
                    return this;
                return m_LinkedSlots[0];
            } 
        }

        public IVFXSlotContainer owner { get { return m_Owner as IVFXSlotContainer; } }

        public VFXSlot GetTopMostParent() // TODO Cache this instead of walking the hierarchy every time
        {
            if (GetParent() == null)
                return this;
            else
                return GetParent().GetTopMostParent();
        }

        protected VFXSlot() {} // For serialization only

        // Create and return a slot hierarchy from a property info
        public static VFXSlot Create(VFXProperty property, Direction direction, object value = null)
        {
            var slot = CreateSub(property, direction, value); // First create slot tree
            slot.RecomputeExpressionTree(); // Initialize expressions   
            return slot;
        }
     
        private static VFXSlot CreateSub(VFXProperty property, Direction direction, object value)
        {
            var desc = VFXLibrary.GetSlot(property.type);
            if (desc != null)
            {
                var slot = desc.CreateInstance();
                slot.m_Direction = direction;
                slot.m_Property = property;
                slot.m_Value = value;

                foreach (var subInfo in property.SubProperties())
                {
                    var subSlot = CreateSub(subInfo, direction, null /* TODOPAUL : sub operation ? */);
                    if (subSlot != null)
                        subSlot.Attach(slot,false);
                }

                return slot;
            }

            throw new InvalidOperationException(string.Format("Unable to create slot for property {0} of type {1}",property.name,property.type));
        }

        private void InitDefaultExpression()
        {
            if (GetNbChildren() == 0)
            {
                m_DefaultExpression = DefaultExpression();
            }
            else
            {
                // Depth first
                foreach (var child in children)
                    child.InitDefaultExpression();

                m_DefaultExpression = ExpressionFromChildren(children.Select(c => c.m_DefaultExpression).ToArray());
            }

            m_LinkedInExpression = m_InExpression = m_OutExpression = m_DefaultExpression;
        }

        private void ResetExpression()
        {
            if (GetNbChildren() == 0)
                SetExpression(m_DefaultExpression,false);
            else
            {
                foreach (var child in children)
                    child.ResetExpression();
            }  
        }

        protected override void Invalidate(VFXModel model,InvalidationCause cause)
        {
            // do nothing for slots
        }
    
        public int GetNbLinks() { return m_LinkedSlots.Count; }
        public bool HasLink() { return GetNbLinks() != 0; }
        
        public bool CanLink(VFXSlot other)
        {
            return direction != other.direction && !m_LinkedSlots.Contains(other) && 
                ((direction == Direction.kInput && CanConvertFrom(other.expression)) || (other.CanConvertFrom(expression)));
        }

        public bool Link(VFXSlot other, bool notify = true)
        {
            if (other == null)
                return false;

            if (!CanLink(other) || !other.CanLink(this)) // can link
                return false;

            if (direction == Direction.kOutput)
                InnerLink(this, other, notify);
            else
                InnerLink(other, this, notify);

            return true;
        }

        public void Unlink(VFXSlot other, bool notify = true)
        {
            if (m_LinkedSlots.Contains(other))
            {
                InnerUnlink(other,notify);
                other.InnerUnlink(this,notify);
            }
        }

        protected void PropagateToOwner(Action<IVFXSlotContainer> func)
        {
            if (owner != null)
                func(owner);
            else
            {
                var parent = GetParent();
                if (parent != null)
                    parent.PropagateToOwner(func);
            }
        }

        protected void PropagateToParent(Action<VFXSlot> func)
        {
            var parent = GetParent();
            if (parent != null)
            {
                func(parent);
                parent.PropagateToParent(func);   
            }
        }

        protected void PropagateToChildren(Action<VFXSlot> func)
        {
            func(this);
            foreach (var child in children) 
                child.PropagateToChildren(func);
        }

        protected void PropagateToTree(Action<VFXSlot> func)
        {
            PropagateToParent(func);
            PropagateToChildren(func);
        }


        protected IVFXSlotContainer GetOwner()
        {
            var parent = GetParent();
            if (parent != null)
                return parent.GetOwner();
            else
                return owner;
        }

        public void Initialize()
        {
            if (m_Initialize)
                return;

            var roots = new List<IVFXSlotContainer>();
            var visited = new HashSet<IVFXSlotContainer>();
            GatherUninitializedRoots(this.GetOwner(), roots, visited);

            foreach (var container in roots)
                container.UpdateOutputs();
        }

        private static void GatherUninitializedRoots(IVFXSlotContainer currentContainer, List<IVFXSlotContainer> roots, HashSet<IVFXSlotContainer> visited)
        {
            if (visited.Contains(currentContainer))
                return;

            visited.Add(currentContainer);
            if (currentContainer.GetNbInputSlots() == 0)
            {
                roots.Add(currentContainer);
                return;
            }

            foreach (var input in currentContainer.inputSlots)
                if (!input.m_Initialize)
                {
                    var owner = input.GetOwner();
                    if (owner != null)
                        GatherUninitializedRoots(owner, roots, visited);
                }
        }

        private void RecomputeExpressionTree(bool propagate = false,bool notify = true)
        {
            Debug.Log("RECOMPUTE EXPRESSION TREE FOR " + GetType().Name +" " + id);
            // Start from the top most parent
            var masterSlot = GetTopMostParent();

            if (!m_Initialize)
            {
                if (direction == Direction.kInput)
                {
                    var outputs = new HashSet<VFXSlot>();
                    masterSlot.PropagateToChildren(s =>
                    {
                        if (HasLink())
                            outputs.Add(refSlot.GetTopMostParent());
                    });

                    foreach (var output in outputs)
                        if (!output.m_Initialize)
                            output.RecomputeExpressionTree(false, false);
                }

                InitDefaultExpression();
                masterSlot.PropagateToChildren(s => s.m_Initialize = true);
            }

            // First set the linked expression in case of input nodes (For output linked expression is set explicitly)
            if (masterSlot.direction == Direction.kInput) // For input slots, linked expression are directly taken from linked slots
                masterSlot.PropagateToChildren(s =>
                {
                    s.m_LinkedInExpression = s.HasLink() ? s.refSlot.m_OutExpression : s.m_DefaultExpression;
                });

            bool needsRecompute = false;
            masterSlot.PropagateToChildren(s =>
            {
                if (s.m_LinkedInExpression != s.m_CachedLinkedInExpression)
                {
                    s.m_CachedLinkedInExpression = s.m_LinkedInExpression;
                    needsRecompute = true;
                }
            });

            if (!needsRecompute) // We dont need to recompute, tree is already up to date
                return;

            List<VFXSlot> startSlots = new List<VFXSlot>();

            // First set the linked expression in case of input nodes (For output linked expression is set explicitly)
            if (masterSlot.direction == Direction.kInput)
                masterSlot.PropagateToChildren( s => {
                    s.m_LinkedInExpression = s.HasLink() ? s.refSlot.m_OutExpression : s.m_DefaultExpression;
                });

            // Collect linked expression
            masterSlot.PropagateToChildren( s => {
                if (s.m_LinkedInExpression != s.m_DefaultExpression) 
                    startSlots.Add(s); 
            });

            bool earlyOut = true;

            // build expression trees by propagating from start slots
            foreach (var startSlot in startSlots)
            {
                if (!startSlot.CanConvertFrom(startSlot.m_LinkedInExpression))
                    throw new ArgumentException("Cannot convert expression");

                var newExpression = startSlot.ConvertExpression(startSlot.m_LinkedInExpression);
                if (newExpression == startSlot.m_InExpression) // already correct, early out
                    continue;

                earlyOut = false;
                startSlot.m_InExpression = newExpression;
                startSlot.PropagateToParent(s => s.m_InExpression = s.ExpressionFromChildren(s.children.Select(c => c.m_InExpression).ToArray()));

                startSlot.PropagateToChildren(s => {
                    var exp = s.ExpressionToChildren(s.m_InExpression);
                    for (int i = 0; i < s.GetNbChildren(); ++i)
                        s.GetChild(i).m_InExpression = exp != null ? exp[i] : s.refSlot.GetChild(i).expression;
                });
            }

            if (startSlots.Count == 0)
            {
                masterSlot.PropagateToChildren(s =>
                {
                    if (s.m_InExpression != s.m_LinkedInExpression)
                    {
                        s.m_InExpression = s.m_LinkedInExpression; // Must be default expression
                        earlyOut = false;
                    }
                });
            }
                
            if (earlyOut)
                return;

            List<VFXSlot> toPropagate = new List<VFXSlot>();

            // Finally derive output expressions
            if (masterSlot.SetOutExpression(masterSlot.m_InExpression))
                toPropagate.Add(masterSlot);
            masterSlot.PropagateToChildren(s => {
                var exp = s.ExpressionToChildren(s.m_OutExpression);
                for (int i = 0; i < s.GetNbChildren(); ++i)
                {
                    var child = s.GetChild(i);
                    if (child.SetOutExpression(exp != null ? exp[i] : child.m_InExpression))
                        toPropagate.AddRange(child.LinkedSlots);
                }
            });  
 
            // Set expression to be up to date
            //masterSlot.PropagateToChildren( s => s.m_Initialize = true );   

            if (notify && masterSlot.m_Owner != null)
                masterSlot.m_Owner.Invalidate(InvalidationCause.kStructureChanged);

            var dirtyMasterSlots = new HashSet<VFXSlot>(toPropagate.Select(s => s.GetTopMostParent()));
            foreach (var dirtySlot in dirtyMasterSlots)
                dirtySlot.RecomputeExpressionTree(notify);
        }

        private void NotifyOwner()
        {
            PropagateToOwner(o => o.Invalidate(VFXModel.InvalidationCause.kConnectionChanged));
        }

        private bool SetOutExpression(VFXExpression expr)
        {
            if (m_OutExpression != expr)
            {
                m_OutExpression = expr;

                if (direction == Direction.kOutput)
                {
                    var toRemove = LinkedSlots.Where(s => !s.CanConvertFrom(expr)); // Break links that are no more valid
                    foreach (var slot in toRemove) 
                        Unlink(slot);
                }
            }
            return true;
        }

        public void UnlinkAll(bool notify = true)
        {
            var currentSlots = new List<VFXSlot>(m_LinkedSlots);
            foreach (var slot in currentSlots)
                Unlink(slot,notify);
        }

        private static void InnerLink(VFXSlot output,VFXSlot input,bool notify = false)
        {
            input.UnlinkAll(false); // First disconnect any other linked slot
            input.PropagateToTree(s => s.UnlinkAll(false)); // Unlink other links in tree
            
            input.m_LinkedSlots.Add(output);
            output.m_LinkedSlots.Add(input);

            input.RecomputeExpressionTree(false);
        }

        private void InnerUnlink(VFXSlot other, bool notify)
        {
            if (m_LinkedSlots.Remove(other))
            {
                if (direction == Direction.kInput)
                {
                    ResetExpression();

                    if (notify)
                        PropagateToOwner(o => o.Invalidate(VFXModel.InvalidationCause.kConnectionChanged));
                }
                else
                {
                    // TODO
                }
            }
        }

        

        /*protected override void OnInvalidate(VFXModel model, InvalidationCause cause)
        {
            if (cause == VFXModel.InvalidationCause.kStructureChanged)
            {
                if (HasLink())
                    throw new InvalidOperationException();

                SetInExpression(ExpressionFromChildren(children.Select(c => c.m_InExpression).ToArray()));
            }
        }*/

        protected virtual bool CanConvertFrom(VFXExpression expr)
        {
            return expr == null || m_DefaultExpression.ValueType == expr.ValueType;
        }

        protected virtual VFXExpression ConvertExpression(VFXExpression expression)
        {
            return expression;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (m_LinkedSlots == null)
            {
                m_LinkedSlots = new List<VFXSlot>();
            }
        }

        public override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();
            if (m_Value != null)
            {
                m_SerializableValue = SerializationHelper.Serialize(m_Value);
            }
        }

         public override void OnAfterDeserialize()
         {
            base.OnAfterDeserialize();
            if (!m_SerializableValue.Empty)
            {
                m_Value = SerializationHelper.Deserialize<object>(m_SerializableValue, null);
            }
            m_SerializableValue.Clear();
        }

        protected virtual VFXExpression[] ExpressionToChildren(VFXExpression exp)   { return null; }
        protected virtual VFXExpression ExpressionFromChildren(VFXExpression[] exp) { return null; }

        protected virtual VFXValue DefaultExpression() 
        {
            return null; 
        }

        private VFXExpression m_DefaultExpression; // The default expression
        private VFXExpression m_LinkedInExpression; // The current linked expression to the slot
        private VFXExpression m_CachedLinkedInExpression; // Cached footprint of latest recompute tree
        private VFXExpression m_InExpression; // correctly converted expression
        private VFXExpression m_OutExpression; // output expression that can be fetched
        private bool m_Initialize = false;

        private VFXSlot m_MasterSlot;

        [SerializeField]
        public VFXModel m_Owner;

        [SerializeField]
        private VFXProperty m_Property;

        [SerializeField]
        private Direction m_Direction;

        [SerializeField]
        private List<VFXSlot> m_LinkedSlots;

        [SerializeField]
        private SerializationHelper.JSONSerializedElement m_SerializableValue;

        [NonSerialized]
        protected object m_Value;
    }
}
