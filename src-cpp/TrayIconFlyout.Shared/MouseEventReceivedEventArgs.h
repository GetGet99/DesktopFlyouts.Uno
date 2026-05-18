#pragma once

#include "Libraries.MouseEventReceivedEventArgs.g.h"

namespace winrt::U5BFA::Libraries::implementation
{
    struct MouseEventReceivedEventArgs : MouseEventReceivedEventArgsT<MouseEventReceivedEventArgs>
    {
        explicit MouseEventReceivedEventArgs(winrt::Windows::Foundation::Point const& point);

        winrt::Windows::Foundation::Point Point() const noexcept;

    private:
        winrt::Windows::Foundation::Point m_point{};
    };
}

namespace winrt::U5BFA::Libraries::factory_implementation
{
    struct MouseEventReceivedEventArgs : MouseEventReceivedEventArgsT<MouseEventReceivedEventArgs, implementation::MouseEventReceivedEventArgs>
    {
    };
}
