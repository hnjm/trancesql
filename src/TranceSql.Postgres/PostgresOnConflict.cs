﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TranceSql.Postgres
{

    /// <summary>
    /// Defines an optional ON CONFLICT clause to specify an alternative action
    /// to raising a unique violation or exclusion constraint violation error.
    /// </summary>
    public class PostgresOnConflict : ISqlElement
    {
        private ColumnCollection _target;

        /// <summary>
        /// Specifies which conflicts ON CONFLICT takes the alternative action
        /// on by choosing arbiter indexes. This should be either a constraint
        /// name or columns to infer the constraint from.
        /// </summary>
        public ColumnCollection Target
        {
            get => _target = _target ?? new ColumnCollection();
            set => _target = value;
        }

        /// <summary>
        /// The operation to perform on conflict, if this value is null, a DO
        /// NOTHING operation will be specified.
        /// </summary>
        public Update DoUpdate { get; set; }

        void ISqlElement.Render(RenderContext context)
        {
            if (DoUpdate == null)
            {
                context.WriteLine("ON CONFLICT DO NOTHING");
            }
            else
            {
                if (_target?.Any() == true)
                {
                    context.Write($"ON CONFLICT (");
                    context.RenderDelimited(_target);
                    context.WriteLine(") DO UPDATE SET");
                }
                else
                {
                    context.WriteLine("ON CONFLICT DO UPDATE SET");
                }

                using (context.EnterChildMode(RenderMode.Nested))
                {
                    context.RenderDelimited(DoUpdate.Set);
                }

                if (DoUpdate.Where.Any() == true)
                {
                    context.WriteLine();
                    context.Write("WHERE ");
                    context.Render(DoUpdate.Where);
                }
            }
        }
    }
}
