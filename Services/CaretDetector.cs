using System;
using System.Windows.Automation;

namespace Clipwell
{
    // Проверяет, стоит ли фокус в поле ввода текста.
    // Используется интерфейс доступности Windows (UI Automation): он видит поля в браузерах и современных программах.
    public static class CaretDetector
    {
        public static string Describe()
        {
            try
            {
                AutomationElement element = AutomationElement.FocusedElement;
                if (element == null)
                {
                    return "нет фокуса";
                }
                return element.Current.ControlType.ProgrammaticName;
            }
            catch (Exception ex)
            {
                return "ошибка: " + ex.Message;
            }
        }

        public static bool HasEditableFocus()
        {
            try
            {
                AutomationElement element = AutomationElement.FocusedElement;
                if (element == null)
                {
                    return false;
                }

                bool? readOnly = IsReadOnly(element);
                if (readOnly == true)
                {
                    return false;
                }

                ControlType type = element.Current.ControlType;
                if (type == ControlType.Edit)
                {
                    return true;
                }

                // Документы (Word, редакторы) считаем полем ввода только если у них есть значение, которое можно менять
                return type == ControlType.Document && readOnly == false;
            }
            catch
            {
                return false;
            }
        }

        // true — только для чтения, false — можно редактировать, null — не сказано
        private static bool? IsReadOnly(AutomationElement element)
        {
            object pattern;
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
            {
                return ((ValuePattern)pattern).Current.IsReadOnly;
            }
            return null;
        }
    }
}
