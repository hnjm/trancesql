﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TranceSql
{
    /// <summary>
    /// Represents a DELETE FROM statement.
    /// </summary>
    public class Delete : ISqlStatement
    {
        private AnyOf<Table, Alias, ISqlElement> _from;
        
        /// <summary>
        /// Gets or sets table to delete from.
        /// </summary>
        public Any<Table, Alias, string> From
        {
            get
            {
                switch (_from?.Value)
                {
                    case Table table: return table;
                    case Alias alias: return alias;
                    default: return null;
                }
            }
            set
            {
                switch (value?.Value)
                {
                    case string name:
                        _from = new Table(name);
                        break;
                    case Table table:
                        _from = table;
                        break;
                    case Alias alias:
                        _from = alias;
                        break;
                    default:
                        _from = null;
                        break;
                }
            }
        }

        private ConditionCollection _where;
        /// <summary>
        /// Gets or sets the where filter for this statement.
        /// </summary>
        public ConditionCollection Where
        {
            get => _where = _where ?? new ConditionCollection();
            set => _where = value;
        }

        void ISqlElement.Render(RenderContext context)
        {
            using (context.EnterChildMode(RenderMode.Nested))
            {
                context.Write("DELETE FROM ");
                context.Render(_from.Value);
                if (_where?.Any() == true)
                {
                    context.WriteLine();
                    context.Write("WHERE ");
                    context.Render(_where);
                }
                context.Write(';');
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString() => this.RenderDebug();
    }
}