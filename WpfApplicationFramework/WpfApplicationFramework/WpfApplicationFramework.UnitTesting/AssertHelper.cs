﻿using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Input;

namespace System.Waf.UnitTesting
{
    /// <summary>
    /// This class contains assert methods which can be used in unit tests.
    /// </summary>
    public static class AssertHelper
    {
        /// <summary>
        /// Asserts that the execution of the provided action throws the specified exception.
        /// </summary>
        /// <typeparam name="T">The expected exception type</typeparam>
        /// <param name="action">The action to execute.</param>
        /// <exception cref="AssertException">This exception is thrown when the expected exception was
        /// not thrown by the action.</exception>
        public static void ExpectedException<T>(Action action) where T : Exception
        {
            if (action == null) { throw new ArgumentNullException("action"); }
            
            bool expectedExceptionThrown = false;
            Exception thrownException = null;
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(T))
                {
                    expectedExceptionThrown = true;
                }
                thrownException = e;
            }

            if (!expectedExceptionThrown)
            {
                if (thrownException != null)
                {
                    throw new AssertException(string.Format(null, 
                        "Test method threw exception {0}, but exception {1} was expected. "
                        + "Exception message: {0}: {2}", 
                        thrownException.GetType().Name, typeof(T).Name, thrownException.Message));
                }
                else
                {
                    throw new AssertException(string.Format(null, 
                        "Test method did not throw expected exception {0}", typeof(T).Name));
                }
            }
        }

        /// <summary>
        /// Asserts that the execution of the provided action raises the CanExecuteChanged event of the command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="raiseCanExecuteChanged">An action that results in a CanExecuteChanged event of the command.</param>
        /// <exception cref="AssertException">This exception is thrown when no or more than one CanExecuteChanged event was 
        /// raised by the command or the sender object of the event was not the command object.</exception>
        public static void CanExecuteChangedEvent(ICommand command, Action raiseCanExecuteChanged)
        {
            if (command == null) { throw new ArgumentNullException("command"); }
            if (raiseCanExecuteChanged == null) { throw new ArgumentNullException("raiseCanExecuteChanged"); }
            
            int canExecuteChangedCount = 0;

            EventHandler canExecuteChangedHandler = (object sender, EventArgs e) =>
            {
                if (command != sender)
                {
                    throw new AssertException("The sender object of the event isn't the command");
                }

                canExecuteChangedCount++;
            };

            command.CanExecuteChanged += canExecuteChangedHandler;
            raiseCanExecuteChanged();
            command.CanExecuteChanged -= canExecuteChangedHandler;

            if (canExecuteChangedCount < 1)
            {
                throw new AssertException(string.Format(null, "The CanExecuteChanged event wasn't raised."));
            }
            else if (canExecuteChangedCount > 1)
            {
                throw new AssertException(string.Format(null, "The CanExecuteChanged event was raised more than once."));
            }
        }

        /// <summary>
        /// Asserts that the execution of the provided action raises the property changed event.
        /// </summary>
        /// <typeparam name="T">The type of the observable.</typeparam>
        /// <param name="observable">The observable which should raise the property changed event.</param>
        /// <param name="expression">A simple expression which identifies the property (e.g. x => x.Name).</param>
        /// <param name="raisePropertyChanged">An action that results in a property changed event of the observable.</param>
        /// <exception cref="AssertException">This exception is thrown when no or more than one property changed event was 
        /// raised by the observable or the sender object of the event was not the observable object.</exception>
        public static void PropertyChangedEvent<T>(T observable, Expression<Func<T, object>> expression, Action raisePropertyChanged)
            where T : class, INotifyPropertyChanged
        {
            if (observable == null) { throw new ArgumentNullException("observable"); }
            if (expression == null) { throw new ArgumentNullException("expression"); }
            if (raisePropertyChanged == null) { throw new ArgumentNullException("raisePropertyChanged"); }
            
            string propertyName = GetProperty(expression).Name;
            int propertyChangedCount = 0;

            PropertyChangedEventHandler propertyChangedHandler = (object sender, PropertyChangedEventArgs e) =>
            {
                if (observable != sender)
                {
                    throw new AssertException("The sender object of the event isn't the observable");
                }

                if (e.PropertyName == propertyName)
                {
                    propertyChangedCount++;
                }
            };

            observable.PropertyChanged += propertyChangedHandler;
            raisePropertyChanged();
            observable.PropertyChanged -= propertyChangedHandler;

            if (propertyChangedCount < 1)
            {
                throw new AssertException(string.Format(null, 
                    "The PropertyChanged event for the property '{0}' wasn't raised.", propertyName));
            }
            else if (propertyChangedCount > 1)
            {
                throw new AssertException(string.Format(null,
                    "The PropertyChanged event for the property '{0}' was raised more than once.", propertyName));
            }
        }


        private static PropertyInfo GetProperty<T>(Expression<Func<T, object>> propertyExpression)
        {
            Expression expression = propertyExpression.Body;

            // If the Property returns a ValueType then a Convert is required => Remove it
            if (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
            }

            // If this isn't a member access expression then the expression isn't valid
            MemberExpression memberExpression = expression as MemberExpression;
            if (memberExpression == null)
            {
                ThrowExpressionArgumentException();
            }

            expression = memberExpression.Expression;

            // If the Property returns a ValueType then a Convert is required => Remove it
            if (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
            }

            // Check if the expression is the parameter itself
            if (expression.NodeType != ExpressionType.Parameter)
            {
                ThrowExpressionArgumentException();
            }

            // Finally retrieve the PropertyInfo
            PropertyInfo propertyInfo = memberExpression.Member as PropertyInfo;
            if (propertyInfo == null)
            {
                ThrowExpressionArgumentException();
            }

            return propertyInfo;
        }

        private static void ThrowExpressionArgumentException()
        {
            throw new ArgumentException("Only the simple expression 'x => x.[Property]' is allowed. "
                + "[Property] must be replaced with the property name that should be used.", "propertyExpression");
        }
    }
}
