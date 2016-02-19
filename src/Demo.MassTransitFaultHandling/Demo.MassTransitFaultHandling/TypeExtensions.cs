using System;

namespace Demo.MassTransitFaultHandling
{
    public static class TypeExtensions
    {
        static readonly TypeNameFormatter TypeNameFormatter = new TypeNameFormatter();

        /// <summary>
        /// Returns an easy-to-read type name from the specified Type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetTypeName(this Type type)
        {
            return TypeNameFormatter.GetTypeName(type);
        }
    }
}