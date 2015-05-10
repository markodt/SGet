using System;

namespace SGet
{
    public class PropertyModel
    {
        private string _name = String.Empty;
        private string _value = String.Empty;

        public PropertyModel(string name, string value)
        {
            _name = name;
            _value = value;
        }

        public string Name
        {
            get { return _name; }
        }

        public string Value
        {
            get { return _value; }
        }
    }
}
