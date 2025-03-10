#pragma once
#include <cstdint>
#include <stdexcept>

namespace Updater::Data
{
    class Convert final
    {
    public:

        /// <summary>
        /// Converts the specified size_t value to a 32-bit signed integer.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>A 32-bit signed integer that is equivalent to value.</returns>
        static int32_t toInt32(size_t value)
        {
            if (value > INT32_MAX)
                throw std::overflow_error::exception();

            return static_cast<int32_t>(value);
        }

        /// <summary>
        /// Converts the specified size_t value to a 32-bit unsigned integer.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>A 32-bit unsigned integer that is equivalent to value.</returns>
        static uint32_t toUInt32(size_t value)
        {
            if (value > UINT32_MAX)
                throw std::overflow_error::exception();

            return static_cast<uint32_t>(value);
        }
    };
}
